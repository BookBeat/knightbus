using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Singleton;

/// <summary>
/// Advises which host should hold which singleton lock, so the locks are spread over the hosts
/// instead of piling up on the first one to start. The advice is not binding: the lock itself
/// still decides who processes, and a placement that has no information returns no advice.
/// </summary>
public interface ISingletonPlacement
{
    /// <summary>
    /// Identifies this host among the others sharing the locks
    /// </summary>
    string HostId { get; }

    /// <summary>
    /// Joins the group of hosts and starts following who else is in it
    /// </summary>
    /// <param name="locks">The singleton locks this host can run, with how heavy each one is</param>
    /// <param name="cancellationToken">Cancels joining</param>
    Task StartAsync(IReadOnlyDictionary<string, int> locks, CancellationToken cancellationToken);

    /// <summary>
    /// Leaves the group, so the other hosts stop counting on this one
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The id of the host that should hold the lock, compared with <see cref="HostId"/>, or null
    /// when there is no reliable information, in which case the caller should behave as if there
    /// were no placement
    /// </summary>
    string? Advise(string lockId);
}
