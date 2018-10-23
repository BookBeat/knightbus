using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Singleton
{
    public interface ISingletonLockHandle
    {
        string LeaseId { get; }
        string LockId { get; }
        Task<bool> RenewAsync(ILog log, CancellationToken cancellationToken);
        Task ReleaseAsync(CancellationToken cancellationToken);
    }
}