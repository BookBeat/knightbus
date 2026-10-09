using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Singleton;

/// <summary>
/// Implemented by a lock manager that can tell whether a lock is held without taking it. Singleton
/// placement needs it to know how long a lock has been free before a host that is not the preferred
/// holder may take it.
/// </summary>
public interface ISingletonLockInspector
{
    /// <summary>
    /// True when someone holds the lock right now
    /// </summary>
    Task<bool> IsHeldAsync(string lockId, CancellationToken cancellationToken);
}
