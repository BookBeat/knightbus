using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Singleton;

/// <summary>
/// Keeps track of the hosts that share the singleton locks, so the locks can be spread over them.
/// A host is a member while it keeps its membership alive, and one that dies drops out by itself.
/// </summary>
public interface ISingletonMembership
{
    /// <summary>
    /// Identifies this host among the members and in the logs
    /// </summary>
    string HostId { get; }

    /// <summary>
    /// Makes this host a member that can run these locks until <see cref="LeaveAsync"/>. It is
    /// called again on every refresh: it returns at once while the membership is alive, and joins
    /// again when it was lost
    /// </summary>
    /// <param name="locks">The singleton locks this host can run, with how heavy each one is</param>
    /// <param name="cancellationToken">Cancels joining</param>
    Task JoinAsync(IReadOnlyDictionary<string, int> locks, CancellationToken cancellationToken);

    /// <summary>
    /// The other live members, not this host, with the locks each can run. A member that cannot be
    /// read is left out
    /// </summary>
    Task<IReadOnlyCollection<SingletonMember>> GetMembersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops being a member, so the other hosts stop counting on this one. The membership is no
    /// longer kept alive even when the token is already cancelled, and leaving is safe after a
    /// failed join
    /// </summary>
    Task LeaveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A live host and the singleton locks it can run, with the weight of each lock
/// </summary>
public sealed record SingletonMember(string HostId, IReadOnlyDictionary<string, int> Locks);
