using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisMessageStateHandler<T> : IMessageStateHandler<T> where T : class, IMessage, IRedisCommand
    {
        private readonly IConnectionMultiplexer _connection;
        private readonly RedisConfiguration _configuration;
        private readonly RedisMessage<T> _redisMessage;
        private readonly string _queueName;

        public RedisMessageStateHandler(IConnectionMultiplexer connection, RedisConfiguration configuration, RedisMessage<T> redisMessage, IMessageSerializer serializer, int deadLetterDeliveryLimit, string queueName)
        {
            _connection = connection;
            _configuration = configuration;
            _redisMessage = redisMessage;
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            _queueName = queueName;
        }

        public int DeliveryCount => (int)_redisMessage.HashEntries.Single(h => h.Name == RedisHashKeys.DeliveryCount).Value;
        public int DeadLetterDeliveryLimit { get; }

        public IDictionary<string, string> MessageProperties { get; }
        public Task CompleteAsync()
        {
            var db = _connection.GetDatabase(_configuration.DatabaseId);
            return db.HashDeleteAsync(_redisMessage.HashKey, _redisMessage.HashEntries.Select(h => h.Name).ToArray());
        }

        public async Task AbandonByErrorAsync(Exception e)
        {
            var db = _connection.GetDatabase(_configuration.DatabaseId);
            await db.ListRightPushAsync(_queueName, _redisMessage.RedisValue);
        }

        public async Task DeadLetterAsync(int deadLetterLimit)
        {
            var db = _connection.GetDatabase(_configuration.DatabaseId);
            await db.ListRightPushAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName), _redisMessage.RedisValue);
        }

        public Task<T> GetMessageAsync()
        {
            return Task.FromResult(_redisMessage.Message);
        }
    }
}