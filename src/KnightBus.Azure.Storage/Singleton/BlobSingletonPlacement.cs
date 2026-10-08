using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;

namespace KnightBus.Azure.Storage.Singleton;

/// <summary>
/// Spreads singleton locks over the hosts using blob storage. Every host keeps a lease on its own
/// member blob, which holds the locks it can run, and lists the member blobs of its group to find
/// out who else is alive. A member is alive while its blob is leased, so liveness is decided by the
/// storage service and does not depend on the clocks of the hosts.
/// </summary>
internal sealed class BlobSingletonPlacement : ISingletonPlacement
{
    private static readonly TimeSpan MemberLeaseDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MemberRenewalInterval = TimeSpan.FromSeconds(10);

    //A member blob left behind by a host that died is removed once it has been unleased this long
    private static readonly TimeSpan AbandonedMemberAge = TimeSpan.FromMinutes(10);

    private readonly IStorageBusConfiguration _configuration;
    private readonly IBlobLockScheme _lockScheme;
    private readonly SingletonPlacementOptions _options;
    private readonly ILogger _log;
    private readonly BlobLockManager _lockManager;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, int> _locks = new(StringComparer.Ordinal);
    private readonly Dictionary<
        string,
        (string ETag, IReadOnlyDictionary<string, int> Locks)
    > _contentCache = new();

    private BlobContainerClient _container = null!;
    private string _memberPrefix = null!;
    private CancellationTokenSource? _lifetime;
    private SingletonTimerScope? _memberLease;
    private Task? _refreshLoop;
    private Snapshot? _snapshot;
    private readonly Dictionary<string, long> _liveSince = new(StringComparer.Ordinal);

    public BlobSingletonPlacement(
        IStorageBusConfiguration configuration,
        SingletonPlacementOptions options,
        IBlobLockScheme? lockScheme = null,
        ILogger? log = null,
        TimeProvider? timeProvider = null
    )
    {
        options.Validate();
        _time = timeProvider ?? TimeProvider.System;
        _configuration = configuration;
        _options = options;
        _lockScheme = lockScheme ?? new DefaultBlobLockScheme();
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _lockManager = new BlobLockManager(configuration, _lockScheme);
        HostId =
            $"{Environment.MachineName}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant()}";
    }

    public string HostId { get; }

    public void Register(string lockId, int weight) => _locks[lockId] = Math.Max(1, weight);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lockManager.InitializeAsync().ConfigureAwait(false);
        _container = AzureStorageClientFactory.CreateBlobContainerClient(
            _configuration,
            _lockScheme.ContainerName
        );
        _memberPrefix = $"{_lockScheme.Directory}/_members/{_options.Group}/";

        //Content first, then the lease: the lease is what makes the member count as alive
        var memberId = $"_members/{_options.Group}/{HostId}";
        var blob = _container.GetBlobClient($"{_lockScheme.Directory}/{memberId}");
        await _container
            .CreateIfNotExistsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await blob.UploadAsync(
                BinaryData.FromObjectAsJson(_locks.ToDictionary(x => x.Key, x => x.Value)),
                overwrite: true,
                cancellationToken
            )
            .ConfigureAwait(false);

        var handle =
            await _lockManager
                .TryLockAsync(memberId, MemberLeaseDuration, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not lease the member blob {memberId}");

        _lifetime = new CancellationTokenSource();
        _memberLease = new SingletonTimerScope(
            _log,
            handle,
            true,
            MemberRenewalInterval,
            CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token)
        );

        await RefreshAsync(cancellationToken).ConfigureAwait(false);
        var token = _lifetime.Token;
        _refreshLoop = Task.Run(() => RefreshLoop(token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_lifetime == null)
            return;
        _lifetime.Cancel();
        if (_refreshLoop != null)
            await _refreshLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (_memberLease != null)
        {
            //Cancelling the scope releases the lease; wait for that so the others see us leave
            await _memberLease.Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            _memberLease.Dispose();
        }

        try
        {
            await _container
                .GetBlobClient($"{_memberPrefix}{HostId}")
                .DeleteIfExistsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception e)
        {
            //It is cleaned up later by the other hosts
            _log.LogWarning(e, "Failed to remove the member blob of host {HostId}", HostId);
        }

        _lifetime.Dispose();
        _lifetime = null;
        Volatile.Write(ref _snapshot, null);
    }

    public SingletonPlacementAdvice? Advise(string lockId)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (
            snapshot == null
            || _time.GetElapsedTime(snapshot.TakenAt) > _options.EffectiveStaleAfter
        )
            return null;
        if (!snapshot.Assignment.TryGetValue(lockId, out var preferred))
            return null;

        var memberFor = snapshot.LiveSince.TryGetValue(preferred, out var since)
            ? _time.GetElapsedTime(since)
            : TimeSpan.Zero;
        return new SingletonPlacementAdvice(preferred, preferred == HostId, memberFor);
    }

    private async Task RefreshLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.RefreshInterval, cancellationToken).ConfigureAwait(false);
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                //The previous snapshot stays until it goes stale, then the advice stops
                _log.LogWarning(
                    e,
                    "Could not read the singleton placement group {Group}",
                    _options.Group
                );
            }
        }
    }

    /// <summary>
    /// Reads the live members and recomputes the assignment
    /// </summary>
    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var live = new List<(string HostId, string Name, string ETag)>();
        var abandoned = new List<string>();
        await foreach (
            var item in _container
                .GetBlobsAsync(BlobTraits.None, BlobStates.None, _memberPrefix, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            var hostId = item.Name[_memberPrefix.Length..];
            if (item.Properties.LeaseState == LeaseState.Leased)
                live.Add((hostId, item.Name, item.Properties.ETag?.ToString() ?? string.Empty));
            else if (
                item.Properties.LastModified < DateTimeOffset.UtcNow - AbandonedMemberAge
                && hostId != HostId
            )
                abandoned.Add(item.Name);
        }

        var hosts = new List<SingletonHostInfo>();
        foreach (var (hostId, name, etag) in live.Where(x => x.HostId != HostId))
        {
            var locks = await GetLocksAsync(hostId, name, etag, cancellationToken)
                .ConfigureAwait(false);
            if (locks != null)
                hosts.Add(new SingletonHostInfo(hostId, locks));
        }

        //This host is always a member of its own view, even if the listing is behind
        hosts.Add(new SingletonHostInfo(HostId, _locks.ToDictionary(x => x.Key, x => x.Value)));

        var now = _time.GetTimestamp();
        var ids = hosts.Select(h => h.HostId).ToHashSet(StringComparer.Ordinal);
        foreach (var gone in _liveSince.Keys.Where(k => !ids.Contains(k)).ToList())
        {
            _liveSince.Remove(gone);
            _contentCache.Remove(gone);
        }
        foreach (var id in ids)
            _liveSince.TryAdd(id, now);

        Volatile.Write(
            ref _snapshot,
            new Snapshot(
                SingletonAssignment.Compute(hosts, _options.Epsilon),
                new Dictionary<string, long>(_liveSince),
                now
            )
        );

        foreach (var name in abandoned)
            await TryDeleteAbandonedAsync(name, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, int>?> GetLocksAsync(
        string hostId,
        string name,
        string etag,
        CancellationToken cancellationToken
    )
    {
        if (_contentCache.TryGetValue(hostId, out var cached) && cached.ETag == etag)
            return cached.Locks;
        try
        {
            var content = await _container
                .GetBlobClient(name)
                .DownloadContentAsync(cancellationToken)
                .ConfigureAwait(false);
            var locks = JsonSerializer.Deserialize<Dictionary<string, int>>(content.Value.Content);
            if (locks == null)
                return null;
            _contentCache[hostId] = (etag, locks);
            return locks;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            //A member that cannot be read is left out of this round rather than failing the refresh
            _log.LogWarning(e, "Could not read the member blob {Member}", name);
            return null;
        }
    }

    private async Task TryDeleteAbandonedAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await _container
                .GetBlobClient(name)
                .DeleteIfExistsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            //Leased again or removed by another host in the meantime
            _log.LogDebug(e, "Could not remove the abandoned member blob {Member}", name);
        }
    }

    private sealed class Snapshot(
        IReadOnlyDictionary<string, string> assignment,
        IReadOnlyDictionary<string, long> liveSince,
        long takenAt
    )
    {
        public long TakenAt { get; } = takenAt;
        public IReadOnlyDictionary<string, string> Assignment { get; } = assignment;
        public IReadOnlyDictionary<string, long> LiveSince { get; } = liveSince;
    }
}
