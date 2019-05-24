using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.Sagas
{
    public interface ISaga<T> where T : ISagaData
    {
        T Data { get; }
    }

    public interface ISagaData
    {

    }

    public interface ISagaStore
    {
        Task<T> GetSaga<T>(string id);
        Task Create<T>(T saga);
        Task Update<T>(T saga);
        Task Complete(string id);
        Task Fail(string id);
    }

    public class SagaMiddleWare : IMessageProcessorMiddleware
    {
        private readonly IMessageProcessorProvider _processorProvider;
        private readonly ISagaStore _sagaStore;

        public SagaMiddleWare(IMessageProcessorProvider processorProvider, ISagaStore sagaStore)
        {
            _processorProvider = processorProvider;
            _sagaStore = sagaStore;
        }
        public Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            //Is this a saga
            var processor = _processorProvider.GetProcessor<T>(typeof(T));
            //Find Saga or create one 

        }
    }
}
