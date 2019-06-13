using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.Sagas.Exceptions;
using KnightBus.Messages;

namespace KnightBus.Core.Sagas
{
    public class SagaMiddleware : IMessageProcessorMiddleware
    {
        private readonly ISagaStore _sagaStore;
        private bool _initialized;
        private bool _saga;

        public SagaMiddleware(ISagaStore sagaStore)
        {
            _sagaStore = sagaStore;
        }
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            //Is this a saga
            if (!_initialized)
            {
                var processor = pipelineInformation.HostConfiguration.MessageProcessorProvider.GetProcessor<T>(pipelineInformation.ProcessorInterfaceType);
                _saga = processor is ISaga;
                _initialized = true;
            }

             
            if (_saga)
            {
                var processor = pipelineInformation.HostConfiguration.MessageProcessorProvider.GetProcessor<T>(pipelineInformation.ProcessorInterfaceType);
                var sagaType = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor.GetType(), typeof(ISaga<>)).Single();
                var sagaDataType = sagaType.GenericTypeArguments[0];

                var sagaHandlerType = typeof(SagaHandler<,>).MakeGenericType(sagaDataType, typeof(T));
                var sagaHandler = (ISagaHandler)Activator.CreateInstance(sagaHandlerType, _sagaStore, processor, await messageStateHandler.GetMessageAsync().ConfigureAwait(false));
                try
                {
                    await sagaHandler.Initialize().ConfigureAwait(false);
                }
                catch (SagaAlreadyStartedException e)
                {
                    pipelineInformation.HostConfiguration.Log.Information(e, "Saga already started");
                    await messageStateHandler.CompleteAsync().ConfigureAwait(false);
                    return;
                }
            }

            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}