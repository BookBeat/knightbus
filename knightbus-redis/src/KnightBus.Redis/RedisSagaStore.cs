using System;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using KnightBus.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public class RedisSagaStore : ISagaStore
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IRedisConfiguration _configuration;
        private readonly IMessageSerializer _serializer;

        private string GetKey(string partitionKey, string id) => $"sagas:{partitionKey}:{id}";

        public RedisSagaStore(IConnectionMultiplexer connectionMultiplexer, IRedisConfiguration configuration)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _configuration = configuration;
            _serializer = _configuration.MessageSerializer;
        }
        public async Task<T> GetSaga<T>(string partitionKey, string id)
        {
            byte[] saga = await _connectionMultiplexer.GetDatabase(_configuration.DatabaseId).StringGetAsync(GetKey(partitionKey, id)).ConfigureAwait(false);
            if (saga == null) throw new SagaNotFoundException(partitionKey, id);
            return _serializer.Deserialize<T>(saga.AsSpan());
        }

        public async Task<T> Create<T>(string partitionKey, string id, T sagaData, TimeSpan ttl)
        {
            var saga = _serializer.Serialize(sagaData);
            var sagaSaved = await _connectionMultiplexer.GetDatabase(_configuration.DatabaseId).StringSetAsync(GetKey(partitionKey, id), saga, ttl, When.NotExists).ConfigureAwait(false);
            if (!sagaSaved) throw new SagaAlreadyStartedException(partitionKey, id);
            return sagaData;
        }

        public async Task Update<T>(string partitionKey, string id, T sagaData)
        {
            var saga = _serializer.Serialize(sagaData);
            var sagaSaved = await _connectionMultiplexer.GetDatabase(_configuration.DatabaseId).StringSetAsync(GetKey(partitionKey, id), saga, null, When.Exists).ConfigureAwait(false);
            if (!sagaSaved) throw new SagaNotFoundException(partitionKey, id);
        }

        public async Task Complete(string partitionKey, string id)
        {
            var db = _connectionMultiplexer.GetDatabase(_configuration.DatabaseId);
            var saga = await db.StringGetAsync(GetKey(partitionKey, id)).ConfigureAwait(false);
            if (saga.IsNullOrEmpty) throw new SagaNotFoundException(partitionKey, id);
            await db.KeyDeleteAsync(GetKey(partitionKey, id)).ConfigureAwait(false);
        }
    }
}
