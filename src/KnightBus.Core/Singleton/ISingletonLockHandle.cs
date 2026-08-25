using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KnightBus.Core.Singleton;

public interface ISingletonLockHandle
{
    string LeaseId { get; }
    string LockId { get; }
    Task<bool> RenewAsync(ILogger log, CancellationToken cancellationToken);
    Task ReleaseAsync(CancellationToken cancellationToken);
}
