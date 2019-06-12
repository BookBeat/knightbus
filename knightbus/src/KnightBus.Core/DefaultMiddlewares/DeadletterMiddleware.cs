using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.DefaultMiddlewares
{
    public class DeadLetterMiddleware : IMessageProcessorMiddleware
    {
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            if (messageStateHandler.DeliveryCount > messageStateHandler.DeadLetterDeliveryLimit)
            {
                await messageStateHandler.DeadLetterAsync(messageStateHandler.DeadLetterDeliveryLimit).ConfigureAwait(false);
                return;
            }
            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}