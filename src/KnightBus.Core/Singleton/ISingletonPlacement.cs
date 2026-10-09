using System;
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
    /// Announces a singleton lock this host can run and how heavy it is. Call before
    /// <see cref="StartAsync"/>.
    /// </summary>
    void Register(string lockId, int weight);

    /// <summary>
    /// Joins the group of hosts and starts following who else is in it
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Leaves the group, so the other hosts stop counting on this one
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The host that should hold the lock, or null when there is no reliable information, in which
    /// case the caller should behave as if there were no placement
    /// </summary>
    SingletonPlacementAdvice? Advise(string lockId);
}

/// <param name="PreferredHost">The host that should hold the lock</param>
/// <param name="PreferredIsSelf">True when the preferred host is this one</param>
/// <param name="PreferredHostMemberFor">
/// How long the preferred host has been seen as a live member without a break. A host that has only
/// just joined should not be handed locks before the group has settled.
/// </param>
public sealed record SingletonPlacementAdvice(
    string PreferredHost,
    bool PreferredIsSelf,
    TimeSpan PreferredHostMemberFor
);
