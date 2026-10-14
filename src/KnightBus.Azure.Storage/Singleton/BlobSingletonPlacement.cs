using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
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

    private readonly IBlobLockScheme _lockScheme;
    private readonly ILogger _log;
    private readonly BlobLockManager _lockManager;
    private readonly TimeProvider _time;
    private IReadOnlyDictionary<string, int> _locks = new Dictionary<string, int>();
    private readonly Dictionary<
        string,
        (string ETag, IReadOnlyDictionary<string, int> Locks)
    > _contentCache = new();

    private readonly BlobContainerClient _container;
    private readonly string _memberPrefix;
    private readonly string _memberId;
    private CancellationTokenSource? _lifetime;
    private SingletonTimerScope? _memberLease;
    private Task? _refreshLoop;
    private Snapshot? _snapshot;
    private readonly Dictionary<string, long> _unleasedSince = new(StringComparer.Ordinal);

    public BlobSingletonPlacement(
        IStorageBusConfiguration configuration,
        IBlobLockScheme? lockScheme = null,
        ILogger? log = null,
        TimeProvider? timeProvider = null,
        string? hostId = null
    )
    {
        _time = timeProvider ?? TimeProvider.System;
        _lockScheme = lockScheme ?? new DefaultBlobLockScheme();
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _lockManager = new BlobLockManager(configuration, _lockScheme);
        HostId = hostId ?? HostIdentity.Id;
        _container = AzureStorageClientFactory.CreateBlobContainerClient(
            configuration,
            _lockScheme.ContainerName
        );
        //Every member blob name is built like a lock blob name, so a directory written with or
        //without a trailing '/' finds the same blobs
        _memberId = $"_members/{HostId}";
        _memberPrefix = BlobLockManager.BlobName(_lockScheme, "_members/");
    }

    public string HostId { get; }

    //How often the member list is read
    internal TimeSpan RefreshInterval { get; init; } = TimeSpan.FromSeconds(20);

    //The advice is dropped when the member list could not be read for this long, and the hosts
    //compete for the locks as they do without placement
    internal TimeSpan StaleAfter { get; init; } = TimeSpan.FromMinutes(1);

    public async Task StartAsync(
        IReadOnlyDictionary<string, int> locks,
        CancellationToken cancellationToken
    )
    {
        //A host without singleton processors has nothing to place and must not count as a member
        if (locks.Count == 0)
            return;
        _locks = locks.ToDictionary(x => x.Key, x => Math.Max(1, x.Value), StringComparer.Ordinal);
        await _lockManager.InitializeAsync().ConfigureAwait(false);
        var lifetime = new CancellationTokenSource();
        _lifetime = lifetime;

        //Try once now so the receivers get advice from the start. A failure is not final: the
        //refresh loop joins and refreshes again, and until then this host gets no advice and
        //competes for the locks as it does without placement
        try
        {
            await JoinAsync(lifetime.Token, cancellationToken).ConfigureAwait(false);
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
            _log.LogInformation(
                "Host {HostId} joined the singleton placement with {LockCount} singleton processors",
                HostId,
                _locks.Count
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            //Nothing stops the member lease renewal once the start is abandoned
            await lifetime.CancelAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception e)
        {
            _log.LogWarning(
                e,
                "Host {HostId} could not join the singleton placement yet, it keeps trying",
                HostId
            );
        }

        var token = lifetime.Token;
        _refreshLoop = Task.Run(() => RefreshLoop(token), CancellationToken.None);
    }

    private async Task JoinAsync(CancellationToken lifetime, CancellationToken cancellationToken)
    {
        await _container
            .CreateIfNotExistsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        //Content first, then the lease: the lease is what makes the member count as alive
        var blob = _container.GetBlobClient(BlobLockManager.BlobName(_lockScheme, _memberId));
        await blob.UploadAsync(
                BinaryData.FromObjectAsJson(_locks),
                overwrite: true,
                cancellationToken
            )
            .ConfigureAwait(false);

        var handle =
            await _lockManager
                .TryLockAsync(_memberId, MemberLeaseDuration, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not lease the member blob {_memberId}");

        _memberLease = new SingletonTimerScope(
            _log,
            handle,
            true,
            MemberRenewalInterval,
            CancellationTokenSource.CreateLinkedTokenSource(lifetime)
        );
    }

    //The renewal stops for good when the lease is lost, for example after a long pause or an
    //outage, and other hosts may have removed the member blob since. Without a lease the host
    //keeps trying on every refresh, so a rejoin that fails during an outage is not the last one
    internal async Task RejoinIfLostAsync(CancellationToken cancellationToken)
    {
        var lifetime = _lifetime;
        if (lifetime == null || _memberLease is { Completion.IsCompleted: false })
            return;
        _log.LogWarning("Host {HostId} has no member lease, rejoining the placement", HostId);
        _memberLease?.Dispose();
        _memberLease = null;
        await JoinAsync(lifetime.Token, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_lifetime == null)
            return;
        await _lifetime.CancelAsync().ConfigureAwait(false);
        //The waits give up with the stop token: the lease runs out by itself if it is not released
        if (_refreshLoop != null)
            await _refreshLoop
                .WaitAsync(cancellationToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (_memberLease != null)
        {
            //Cancelling the scope releases the lease; wait for that so the others see us leave
            await _memberLease
                .Completion.WaitAsync(cancellationToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (_memberLease.Completion.IsCompleted)
                _memberLease.Dispose();
        }

        try
        {
            await _container
                .GetBlobClient(BlobLockManager.BlobName(_lockScheme, _memberId))
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
        _log.LogInformation("Host {HostId} left the singleton placement", HostId);
    }

    public string? Advise(string lockId)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot == null || _time.GetElapsedTime(snapshot.TakenAt) > StaleAfter)
            return null;
        if (!snapshot.Assignment.TryGetValue(lockId, out var preferred))
            return null;
        return preferred;
    }

    private async Task RefreshLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RefreshInterval, cancellationToken).ConfigureAwait(false);
                await RejoinIfLostAsync(cancellationToken).ConfigureAwait(false);
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                //The previous snapshot stays until it goes stale, then the advice stops
                _log.LogWarning(e, "Could not read the singleton placement members");
            }
        }
    }

    /// <summary>
    /// Reads the live members and recomputes the assignment
    /// </summary>
    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var live = new List<(string HostId, string Name, string ETag)>();
        var abandoned = new List<(string Name, ETag? ETag)>();
        var unleased = new HashSet<string>(StringComparer.Ordinal);
        await foreach (
            var item in _container
                .GetBlobsAsync(BlobTraits.None, BlobStates.None, _memberPrefix, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            var hostId = item.Name[_memberPrefix.Length..];
            if (item.Properties.LeaseState == LeaseState.Leased)
                live.Add((hostId, item.Name, item.Properties.ETag?.ToString() ?? string.Empty));
            else if (hostId != HostId)
            {
                //Lease operations do not change LastModified, so the time unleased is tracked here
                unleased.Add(item.Name);
                var since = _unleasedSince.GetValueOrDefault(item.Name, _time.GetTimestamp());
                _unleasedSince[item.Name] = since;
                if (_time.GetElapsedTime(since) >= AbandonedMemberAge)
                    abandoned.Add((item.Name, item.Properties.ETag));
            }
        }

        foreach (var name in _unleasedSince.Keys.Where(k => !unleased.Contains(k)).ToList())
            _unleasedSince.Remove(name);

        var hosts = new List<SingletonHostInfo>();
        foreach (var (hostId, name, etag) in live.Where(x => x.HostId != HostId))
        {
            var locks = await GetLocksAsync(hostId, name, etag, cancellationToken)
                .ConfigureAwait(false);
            if (locks != null)
                hosts.Add(new SingletonHostInfo(hostId, locks));
        }

        //This host is always a member of its own view, even if the listing is behind
        hosts.Add(new SingletonHostInfo(HostId, _locks));

        var ids = hosts.Select(h => h.HostId).ToHashSet(StringComparer.Ordinal);
        foreach (var gone in _contentCache.Keys.Where(k => !ids.Contains(k)).ToList())
            _contentCache.Remove(gone);

        Volatile.Write(
            ref _snapshot,
            new Snapshot(SingletonAssignment.Compute(hosts), _time.GetTimestamp())
        );

        foreach (var (name, etag) in abandoned)
            await TryDeleteAbandonedAsync(name, etag, cancellationToken).ConfigureAwait(false);
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
        catch (Exception e)
            when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            //A member that cannot be read is left out of this round rather than failing the refresh
            _log.LogWarning(e, "Could not read the member blob {Member}", name);
            return null;
        }
    }

    //Only the blob as it was listed is removed. A host that rejoins writes its blob again before it
    //leases it, and deleting it in between would make it lease an empty blob the others cannot read
    internal async Task TryDeleteAbandonedAsync(
        string name,
        ETag? listedETag,
        CancellationToken cancellationToken
    )
    {
        if (listedETag == null)
            return;
        try
        {
            await _container
                .GetBlobClient(name)
                .DeleteIfExistsAsync(
                    conditions: new BlobRequestConditions { IfMatch = listedETag },
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception e)
            when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            //Written again, leased again or removed by another host in the meantime
            _log.LogDebug(e, "Could not remove the abandoned member blob {Member}", name);
        }
    }

    private sealed class Snapshot(IReadOnlyDictionary<string, string> assignment, long takenAt)
    {
        public long TakenAt { get; } = takenAt;
        public IReadOnlyDictionary<string, string> Assignment { get; } = assignment;
    }
}
