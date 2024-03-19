using System;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Singleton;

public interface ISingletonLockManager
{
    Task<ISingletonLockHandle> TryLockAsync(string lockId, TimeSpan lockPeriod, CancellationToken cancellationToken);
    Task InitializeAsync();
}
