using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.Sagas
{
    public interface ISaga
    {

    }
    public interface ISaga<T> : ISaga where T : ISagaData
    {
        /// <summary>
        /// Stateful data associated with the Saga
        /// </summary>
        T Data { get; set; }
        /// <summary>
        /// Map a message to a Saga, all messages must be mapped
        /// </summary>
        void MapMessage<TMessage>(Func<TMessage, string> mapping) where TMessage : IMessage;
        void MapStartMessage<TMessage>(Func<TMessage, string> mapping) where TMessage : IMessage;
        /// <summary>
        /// Retrieve a mapping function for a message
        /// </summary>
        Func<IMessage, string> GetMapping<TMessage>() where TMessage : IMessage;
        Func<IMessage, string> GetStartMapping<TMessage>() where TMessage : IMessage;
    }

    public class Saga<T> : ISaga<T> where T : ISagaData
    {
        public T Data { get; set; }
        private Dictionary<Type, Func<IMessage, string>> _mappings = new Dictionary<Type, Func<IMessage, string>>();
        private Func<IMessage, string> _startMapping;

        public void MapMessage<TMessage>(Func<TMessage, string> mapping) where TMessage : IMessage
        {
            if (!_mappings.ContainsKey(typeof(TMessage)))
            {
                _mappings.Add(typeof(TMessage), mapping as Func<IMessage, string>);
            }
        }

        public void MapStartMessage<TMessage>(Func<TMessage, string> mapping) where TMessage : IMessage
        {
            _startMapping = mapping as Func<IMessage, string>;
        }

        public Func<IMessage, string> GetMapping<TMessage>() where TMessage : IMessage
        {
            return _mappings[typeof(TMessage)];
        }

        public Func<IMessage, string> GetStartMapping<TMessage>() where TMessage : IMessage
        {
            return _startMapping;
        }
    }

    public interface IStartSaga
    {

    }

    public interface ISagaData
    {

    }

    public interface ISagaStore<T> where T : ISagaData
    {
        Task<T> GetSaga<T>(string id);
        Task<T> Create<T>(string id, T sagaData);
        Task Update<T>(T saga);
        Task Complete(string id);
        Task Fail(string id);
    }

    public class SagaAlreadyStartedException : Exception { }
    public class SagaNotFoundStartedException : Exception { }

    public class SagaMiddleWare : IMessageProcessorMiddleware
    {
        private readonly IMessageProcessorProvider _processorProvider;
        private readonly ISagaStore _sagaStore;

        public SagaMiddleWare(IMessageProcessorProvider processorProvider, ISagaStore sagaStore)
        {
            _processorProvider = processorProvider;
            _sagaStore = sagaStore;
        }
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            //Is this a saga
            var processor = _processorProvider.GetProcessor<T>(typeof(T));
            //Find Saga or create one 
            if (processor is ISaga)
            {
                var id = "";
                var sagaType = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor.GetType(), typeof(ISaga<>)).Single();
                var sagaDataType = sagaType.GenericTypeArguments[0];

                var sagaHandlerType = typeof(SagaHandler<,>).MakeGenericType(sagaDataType, typeof(T));
                var sagaHandler = Activator.CreateInstance(sagaHandlerType, _sagaStore, processor, await messageStateHandler.GetMessageAsync().ConfigureAwait(false));

                var sagaData = await _sagaStore.GetSaga(sagaDataType, id).ConfigureAwait(false);
                if (typeof(IStartSaga).IsAssignableFrom(typeof(T)))
                {
                    //Saga should start
                    await _sagaStore.Create(sagaDataType, id);
                }
            }

            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
    }

    public class SagaHandler<TSagaData, TMessage> where TSagaData : ISagaData, new() where TMessage : IMessage
    {
        private readonly ISagaStore<TSagaData> _sagaStore;
        private readonly ISaga<TSagaData> _saga;
        private readonly TMessage _message;

        public SagaHandler(ISagaStore<TSagaData> sagaStore, ISaga<TSagaData> saga, TMessage message)
        {
            _sagaStore = sagaStore;
            _saga = saga;
            _message = message;
        }

        public async Task Initialize()
        {
            TSagaData sagaData;
            var id = _saga.GetMapping<TMessage>().Invoke(_message);

            if (typeof(IStartSaga).IsAssignableFrom(typeof(TMessage)))
            {
                sagaData = await _sagaStore.Create<TSagaData>(id, new TSagaData());
            }
            else
            {
                sagaData = await _sagaStore.GetSaga<TSagaData>(id);
            }

            _saga.Data = sagaData;
        }
    }




    public class SampleSaga : Saga<SampleSagaData>, IProcessMessage<BeginEncoding>
    {
        public SampleSaga()
        {
            MapMessage<BeginEncoding>(m => m.Id.ToString());
        }

        public Task ProcessAsync(BeginEncoding message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    public class SampleSagaData : ISagaData
    {
        public string Message { get; set; }
    }

    public class BeginEncoding : IMessage
    {
        public Guid Id { get; set; }
    }
}
