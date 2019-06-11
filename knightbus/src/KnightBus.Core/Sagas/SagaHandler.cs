using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.Sagas
{
    public class SagaHandler<TSagaData, TMessage> where TSagaData : new() where TMessage : IMessage
    {
        private readonly ISagaStore _sagaStore;
        private readonly ISaga<TSagaData> _saga;
        private readonly TMessage _message;

        public SagaHandler(ISagaStore sagaStore, ISaga<TSagaData> saga, TMessage message)
        {
            _sagaStore = sagaStore;
            _saga = saga;
            _message = message;
        }

        public async Task Initialize()
        {
            _saga.SagaStore = _sagaStore;
            TSagaData sagaData;
            _saga.Id = _saga.MessageMapper.GetMapping<TMessage>().Invoke(_message);
            if (_saga.MessageMapper.IsStartMessage(typeof(TMessage)))
            {
                sagaData = await _sagaStore.Create(_saga.PartitionKey, _saga.Id, new TSagaData());
            }
            else
            {
                sagaData = await _sagaStore.GetSaga<TSagaData>(_saga.PartitionKey, _saga.Id);
            }

            _saga.Data = sagaData;
        }
    }
}