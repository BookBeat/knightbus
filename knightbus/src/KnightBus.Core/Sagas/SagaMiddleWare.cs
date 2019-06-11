using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.Sagas
{
    public class SagaMiddleware : IMessageProcessorMiddleware
    {
        private readonly IMessageProcessorProvider _processorProvider;
        private readonly ISagaStore _sagaStore;

        public SagaMiddleware(IMessageProcessorProvider processorProvider, ISagaStore sagaStore)
        {
            _processorProvider = processorProvider;
            _sagaStore = sagaStore;
        }
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            //Is this a saga
            var processor = _processorProvider.GetProcessor<T>(typeof(IProcessMessage<T>));
            //Find Saga or create one 
            if (processor is ISaga)
            {
                var sagaType = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor.GetType(), typeof(ISaga<>)).Single();
                var sagaDataType = sagaType.GenericTypeArguments[0];

                var sagaHandlerType = typeof(SagaHandler<,>).MakeGenericType(sagaDataType, typeof(T));
                var sagaHandler = Activator.CreateInstance(sagaHandlerType, _sagaStore, processor, await messageStateHandler.GetMessageAsync().ConfigureAwait(false));
            }

            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}