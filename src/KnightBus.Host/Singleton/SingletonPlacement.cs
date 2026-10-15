using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host.Singleton;

/// <summary>
/// Spreads singleton locks over the hosts. Every host reads the same members from the
/// <see cref="ISingletonMembership"/> and computes the same assignment, so no coordinator is needed.
/// </summary>
internal sealed class SingletonPlacement : ISingletonPlacement
{
    private readonly ISingletonMembership _membership;
    private readonly ILogger _log;
    private readonly TimeProvider _time;
    private IReadOnlyDictionary<string, int> _locks = new Dictionary<string, int>();
    private CancellationTokenSource? _lifetime;
    private Task? _refreshLoop;
    private Snapshot? _snapshot;

    public SingletonPlacement(
        ISingletonMembership membership,
        ILogger log,
        TimeProvider? timeProvider = null
    )
    {
        _membership = membership;
        _log = log;
        _time = timeProvider ?? TimeProvider.System;
    }

    public string HostId => _membership.HostId;

    //How often the members are read
    internal TimeSpan RefreshInterval { get; init; } = TimeSpan.FromSeconds(20);

    //The advice is dropped when the members could not be read for this long, and the hosts
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
        _locks = locks;
        var lifetime = new CancellationTokenSource();
        _lifetime = lifetime;

        //Try once now so the receivers get advice from the start. A failure is not final: the
        //refresh loop joins and refreshes again, and until then this host gets no advice and
        //competes for the locks as it does without placement
        var joined = false;
        try
        {
            await _membership.JoinAsync(_locks, cancellationToken).ConfigureAwait(false);
            joined = true;
            _log.LogInformation(
                "Host {HostId} joined the singleton placement with {LockCount} singleton processors",
                HostId,
                _locks.Count
            );
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (!joined && !IsCancelled(e, cancellationToken))
        {
            _log.LogWarning(
                e,
                "Host {HostId} could not join the singleton placement yet, it keeps trying",
                HostId
            );
        }
        catch (Exception e) when (!IsCancelled(e, cancellationToken))
        {
            _log.LogWarning(
                e,
                "Host {HostId} could not read the singleton placement members yet, it keeps trying",
                HostId
            );
        }

        var token = lifetime.Token;
        _refreshLoop = Task.Run(() => RefreshLoop(token), CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var lifetime = _lifetime;
        if (lifetime == null)
            return;
        await lifetime.CancelAsync().ConfigureAwait(false);
        //The wait gives up with the stop token, leaving does not
        if (_refreshLoop != null)
            await _refreshLoop
                .WaitAsync(cancellationToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        try
        {
            await _membership.LeaveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            //The membership runs out by itself
            _log.LogWarning(e, "Host {HostId} could not leave the singleton placement", HostId);
        }

        lifetime.Dispose();
        _lifetime = null;
        Volatile.Write(ref _snapshot, null);
        _log.LogInformation("Host {HostId} left the singleton placement", HostId);
    }

    public string? Advise(string lockId)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot == null || _time.GetElapsedTime(snapshot.TakenAt) > StaleAfter)
            return null;
        return snapshot.Assignment.GetValueOrDefault(lockId);
    }

    private async Task RefreshLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RefreshInterval, cancellationToken).ConfigureAwait(false);
                //Joins again when the membership was lost, for example after a long pause or an outage
                await _membership.JoinAsync(_locks, cancellationToken).ConfigureAwait(false);
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
        var members = await _membership.GetMembersAsync(cancellationToken).ConfigureAwait(false);
        //This host is always a member of its own view, even before the others can see it
        var hosts = members.Append(new SingletonMember(HostId, _locks)).ToList();
        Volatile.Write(
            ref _snapshot,
            new Snapshot(SingletonAssignment.Compute(hosts), _time.GetTimestamp())
        );
    }

    private static bool IsCancelled(Exception e, CancellationToken cancellationToken) =>
        e is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private sealed record Snapshot(IReadOnlyDictionary<string, string> Assignment, long TakenAt);
}
