using System;
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
                var processor = messageStateHandler.MessageScope.GetInstance<object>(pipelineInformation.ProcessorInterfaceType); 

                if (processor is IProcessBeforeDeadLetter<T> deadletterProcessor)
                {
                    var message = await messageStateHandler.GetMessageAsync().ConfigureAwait(false);
                    try
                    {
                        await deadletterProcessor.BeforeDeadLetterAsync(message, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        pipelineInformation.HostConfiguration.Log.Error(e, "Failed before deadletter processing {@" + typeof(T).Name + "}", message);
                    }
                }

                await messageStateHandler.DeadLetterAsync(messageStateHandler.DeadLetterDeliveryLimit).ConfigureAwait(false);
                return;
            }
            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}