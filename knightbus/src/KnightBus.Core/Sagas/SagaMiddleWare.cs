using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.Sagas.Exceptions;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;

namespace KnightBus.Core.Sagas;

public class SagaMiddleware : IMessageProcessorMiddleware
{
    private readonly ISagaStore _sagaStore;

    public SagaMiddleware(ISagaStore sagaStore)
    {
        _sagaStore = sagaStore;
    }
    public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
    {
        //Is this a saga
        var processor = messageStateHandler.MessageScope.GetInstance<object>(pipelineInformation.ProcessorInterfaceType);

        if (processor is ISaga saga)
        {

            var sagaType = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor.GetType(), typeof(ISaga<>)).Single();
            var sagaDataType = sagaType.GenericTypeArguments[0];

            var sagaHandlerType = typeof(SagaHandler<,>).MakeGenericType(sagaDataType, typeof(T));
            var message = messageStateHandler.GetMessage();
            var sagaHandler = (ISagaHandler)Activator.CreateInstance(sagaHandlerType, _sagaStore, processor, message);
            try
            {
                await sagaHandler.Initialize(cancellationToken).ConfigureAwait(false);
            }
            catch (SagaAlreadyStartedException e)
            {
                // If the processor is ISagaDuplicateDetected, let the processor process the duplicate message before completing the saga
                if (processor is ISagaDuplicateDetected<T> sagaDetectedHandler)
                {
                    await sagaDetectedHandler.ProcessDuplicateAsync(message, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    pipelineInformation.HostConfiguration.Log.LogInformation(e, "Saga already started");
                }

                await messageStateHandler.CompleteAsync().ConfigureAwait(false);
                return;
            }

            try
            {
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                if (saga.MessageMapper.IsStartMessage(typeof(T)))
                {
                    //If we have started a saga but the start message fails then we must make sure the message can be retried
                    await _sagaStore.Delete(saga.PartitionKey, saga.Id, cancellationToken).ConfigureAwait(false);
                }
                throw;
            }
        }
        else
        {
            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}
