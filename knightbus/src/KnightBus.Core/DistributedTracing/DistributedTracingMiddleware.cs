using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.DistributedTracing;

public class DistributedTracingMiddleware : IMessageProcessorMiddleware
{
    public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation,
        IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
    {
        var distributedTracingProvider = messageStateHandler.MessageScope.GetInstance<IDistributedTracingProvider>();

        distributedTracingProvider.Init(messageStateHandler.MessageProperties);
        
        await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
    }
}
