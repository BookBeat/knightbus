using System;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public class RedisSagaStore : ISagaStore
    {
        private readonly IMessageSerializer _serializer;
        private readonly IDatabase _db;

        private string GetKey(string partitionKey, string id) => $"sagas:{partitionKey}:{id}";

        public RedisSagaStore(IConnectionMultiplexer connectionMultiplexer, int dbId, IMessageSerializer serializer)
        {
            _serializer = serializer;
            _db = connectionMultiplexer.GetDatabase(dbId);
        }
        public async Task<T> GetSaga<T>(string partitionKey, string id)
        {
            var saga = await _db.StringGetAsync(GetKey(partitionKey, id)).ConfigureAwait(false);
            if (saga.IsNullOrEmpty) throw new SagaNotFoundException(partitionKey, id);
            return _serializer.Deserialize<T>(saga);
        }

        public async Task<T> Create<T>(string partitionKey, string id, T sagaData, TimeSpan ttl)
        {
            var saga = _serializer.Serialize(sagaData);
            var sagaSaved = await _db.StringSetAsync(GetKey(partitionKey, id), saga, ttl, When.NotExists).ConfigureAwait(false);
            if (!sagaSaved) throw new SagaAlreadyStartedException(partitionKey, id);
            return sagaData;
        }

        public async Task Update<T>(string partitionKey, string id, T sagaData)
        {
            var saga = _serializer.Serialize(sagaData);
            var sagaSaved = await _db.StringSetAsync(GetKey(partitionKey, id), saga, null, When.Exists).ConfigureAwait(false);
            if (!sagaSaved) throw new SagaNotFoundException(partitionKey, id);
        }

        public async Task Complete(string partitionKey, string id)
        {
            await _db.KeyDeleteAsync(GetKey(partitionKey, id)).ConfigureAwait(false);
        }
    }
}