using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.Deduplication;

public class DeduplicationMiddleware(IDeduplicationStore store) : IMessageProcessorMiddleware
{
    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        await next.ProcessAsync(messageStateHandler, cancellationToken);

        if (typeof(IDeduplicatable).IsAssignableFrom(typeof(T)))
        {
            var message = (IDeduplicatable)messageStateHandler.GetMessage();
            if (
                message.DeduplicationWindow == null
                && messageStateHandler.MessageProperties.TryGetValue(
                    DeduplicationPreProcessor.DeduplicationKeyProperty,
                    out var key
                )
            )
            {
                await store.ReleaseAsync(key.ToString()!, cancellationToken);
            }
        }
    }
}
