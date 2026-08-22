using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host;

/// <summary>
/// Counts messages currently being processed by the host. Placed outermost in every
/// pipeline so shutdown can wait for in-flight messages instead of a fixed grace period.
/// </summary>
internal sealed class InFlightMessageTracker : IMessageProcessorMiddleware
{
    private long _inFlight;

    public long Count => Interlocked.Read(ref _inFlight);

    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        Interlocked.Increment(ref _inFlight);
        try
        {
            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }
}
