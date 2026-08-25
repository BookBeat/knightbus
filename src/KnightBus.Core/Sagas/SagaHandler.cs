using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.Sagas;

public interface ISagaHandler
{
    Task Initialize(CancellationToken ct);
}

public class SagaHandler<TData, TMessage> : ISagaHandler
    where TData : new()
    where TMessage : IMessage
{
    private readonly ISagaStore _sagaStore;
    private readonly ISaga<TData> _saga;
    private readonly TMessage _message;

    public SagaHandler(ISagaStore sagaStore, ISaga<TData> saga, TMessage message)
    {
        _sagaStore = sagaStore;
        _saga = saga;
        _message = message;
    }

    public async Task Initialize(CancellationToken ct)
    {
        _saga.SagaStore = _sagaStore;
        SagaData<TData> sagaData;
        _saga.Id = _saga.MessageMapper.GetMapping<TMessage>().Invoke(_message);
        if (_saga.MessageMapper.IsStartMessage(typeof(TMessage)))
        {
            sagaData = await _sagaStore.Create(
                _saga.PartitionKey,
                _saga.Id,
                new TData(),
                _saga.TimeToLive,
                ct
            );
        }
        else
        {
            sagaData = await _sagaStore.GetSaga<TData>(_saga.PartitionKey, _saga.Id, ct);
        }

        _saga.SagaData = sagaData;
    }
}
