using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.Sagas;

/// <summary>
/// Handle the duplicate sagas in the processor before completing the saga
/// </summary>
public interface ISagaDuplicateDetected<T> where T : IMessage
{
    Task ProcessDuplicateAsync(T message, CancellationToken cancellationToken);
}
