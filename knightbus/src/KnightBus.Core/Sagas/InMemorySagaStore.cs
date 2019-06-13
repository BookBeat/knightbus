using System.Collections.Concurrent;
using System.Threading.Tasks;
using KnightBus.Core.Sagas.Exceptions;

namespace KnightBus.Core.Sagas
{
    public class InMemorySagaStore : ISagaStore
    {
        private readonly ConcurrentDictionary<string, object> _sagas = new ConcurrentDictionary<string, object>();

        public Task<T> GetSaga<T>(string partitionKey, string id)
        {
            if (_sagas.TryGetValue(partitionKey + id, out var saga))
            {
                return Task.FromResult((T)saga);
            }

            throw new SagaNotFoundException(partitionKey, id);
        }

        public Task<T> Create<T>(string partitionKey, string id, T sagaData)
        {
            if (_sagas.TryGetValue(partitionKey + id, out var saga))
            {
                throw new SagaAlreadyStartedException(partitionKey, id);
            }

            if (!_sagas.TryAdd(partitionKey + id, sagaData))
            {
                throw new SagaStorageFailedException(partitionKey, id);
            }

            return Task.FromResult(sagaData);
        }

        public Task Update<T>(string partitionKey, string id, T sagaData)
        {

            if (_sagas.TryGetValue(partitionKey + id, out var saga))
            {
                _sagas.TryUpdate(partitionKey + id, sagaData, saga);
                return Task.CompletedTask;
            }

            throw new SagaNotFoundException(partitionKey, id);
        }

        public Task Complete(string partitionKey, string id)
        {
            _sagas.TryRemove(partitionKey + id, out _);
            return Task.CompletedTask;
        }
    }
}