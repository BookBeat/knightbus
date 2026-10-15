using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;

namespace KnightBus.Azure.Storage.Singleton;

/// <summary>
/// Keeps the singleton members in blob storage. Every host keeps a lease on its own member blob,
/// which holds the locks it can run, and lists the member blobs to find out who else is alive. A
/// member is alive while its blob is leased, so liveness is decided by the storage service and does
/// not depend on the clocks of the hosts.
/// </summary>
internal sealed class BlobSingletonMembership : ISingletonMembership
{
    private static readonly TimeSpan MemberLeaseDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MemberRenewalInterval = TimeSpan.FromSeconds(10);

    //A member blob left behind by a host that died is removed once it is unleased and has not been
    //written for this long
    private static readonly TimeSpan AbandonedMemberAge = TimeSpan.FromMinutes(10);

    private readonly IBlobLockScheme _lockScheme;
    private readonly ILogger _log;
    private readonly TimeProvider _time;
    private readonly BlobContainerClient _container;
    private readonly string _memberPrefix;
    private readonly string _memberId;
    private CancellationTokenSource? _lifetime;
    private SingletonTimerScope? _memberLease;

    public BlobSingletonMembership(
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
        HostId = hostId ?? HostIdentity.Id;
        _container = AzureStorageClientFactory.CreateBlobContainerClient(
            configuration,
            _lockScheme.ContainerName
        );
        //Every member blob name is built like a lock blob name, so the listing finds them whether
        //or not the directory ends with '/'
        _memberId = $"_members/{HostId}";
        _memberPrefix = BlobLockManager.BlobName(_lockScheme, "_members/");
    }

    public string HostId { get; }

    public async Task JoinAsync(
        IReadOnlyDictionary<string, int> locks,
        CancellationToken cancellationToken
    )
    {
        if (_memberLease is { Completion.IsCompleted: false })
            return;
        //The renewal stops for good when the lease is lost, for example after a long pause or an
        //outage, and other hosts may have removed the member blob since
        if (_memberLease != null)
        {
            _log.LogWarning("Host {HostId} has no member lease, rejoining the placement", HostId);
            _memberLease.Dispose();
            _memberLease = null;
        }
        _lifetime ??= new CancellationTokenSource();

        await _container
            .CreateIfNotExistsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        //Content first, then the lease: the lease is what makes the member count as alive
        var blob = _container.GetBlobClient(BlobLockManager.BlobName(_lockScheme, _memberId));
        await blob.UploadAsync(
                BinaryData.FromObjectAsJson(locks),
                overwrite: true,
                cancellationToken
            )
            .ConfigureAwait(false);

        var lease = await blob.GetBlobLeaseClient()
            .AcquireAsync(MemberLeaseDuration, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var handle = new BlobLockHandle(
            lease.Value.LeaseId,
            _memberId,
            blob.GetBlobLeaseClient(lease.Value.LeaseId),
            MemberLeaseDuration
        );

        var leaseSource = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _memberLease = new SingletonTimerScope(
            _log,
            handle,
            true,
            MemberRenewalInterval,
            leaseSource
        );
        //The linked source stays registered with the lifetime until it is disposed
        _ = _memberLease.Completion.ContinueWith(_ => leaseSource.Dispose(), TaskScheduler.Default);
    }

    public async Task<IReadOnlyCollection<SingletonMember>> GetMembersAsync(
        CancellationToken cancellationToken
    )
    {
        var live = new List<(string HostId, string Name)>();
        var abandoned = new List<(string Name, ETag? ETag)>();
        await foreach (
            var item in _container
                .GetBlobsAsync(BlobTraits.None, BlobStates.None, _memberPrefix, cancellationToken)
                .ConfigureAwait(false)
        )
        {
            var hostId = item.Name[_memberPrefix.Length..];
            //The placement knows its own locks, and its own blob is never abandoned
            if (hostId == HostId)
                continue;
            if (item.Properties.LeaseState == LeaseState.Leased)
                live.Add((hostId, item.Name));
            //A blob is written only when its host joins, lease operations do not change LastModified.
            //A host that rejoins writes its blob again before it leases it, which makes it recent
            else if (_time.GetUtcNow() - item.Properties.LastModified >= AbandonedMemberAge)
                abandoned.Add((item.Name, item.Properties.ETag));
        }

        var members = new List<SingletonMember>();
        foreach (var (hostId, name) in live)
        {
            var locks = await GetLocksAsync(name, cancellationToken).ConfigureAwait(false);
            if (locks != null)
                members.Add(new SingletonMember(hostId, locks));
        }

        foreach (var (name, etag) in abandoned)
            await TryDeleteAbandonedAsync(name, etag, cancellationToken).ConfigureAwait(false);
        return members;
    }

    public async Task LeaveAsync(CancellationToken cancellationToken)
    {
        var lifetime = _lifetime;
        if (lifetime == null)
            return;
        //Stops the renewal at once, whatever happens to the waits below
        await lifetime.CancelAsync().ConfigureAwait(false);
        if (_memberLease != null)
        {
            //Cancelling the scope releases the lease; wait for that so the others see us leave.
            //The wait gives up with the token, the lease then runs out by itself
            await _memberLease
                .Completion.WaitAsync(cancellationToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (_memberLease.Completion.IsCompleted)
                _memberLease.Dispose();
            _memberLease = null;
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

        lifetime.Dispose();
        _lifetime = null;
    }

    private async Task<IReadOnlyDictionary<string, int>?> GetLocksAsync(
        string name,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var content = await _container
                .GetBlobClient(name)
                .DownloadContentAsync(cancellationToken)
                .ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, int>>(content.Value.Content);
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
}
