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
        private readonly IDatabase db;
        private readonly RedisConfiguration _configuration;
        private readonly RedisMessage<T> _redisMessage;
        private readonly string _queueName;
        

        public RedisMessageStateHandler(IConnectionMultiplexer connection, RedisConfiguration configuration, RedisMessage<T> redisMessage, IMessageSerializer serializer, int deadLetterDeliveryLimit, string queueName)
        {
            db = connection.GetDatabase(configuration.DatabaseId);
            _configuration = configuration;
            _redisMessage = redisMessage;
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            _queueName = queueName;
        }

        public int DeliveryCount => int.Parse(_redisMessage.HashEntries[RedisHashKeys.DeliveryCount]);
        public int DeadLetterDeliveryLimit { get; }

        public IDictionary<string, string> MessageProperties => _redisMessage.HashEntries;
        public Task CompleteAsync()
        {
            return db.HashDeleteAsync(_redisMessage.HashKey, _redisMessage.HashEntries.Select(h => (RedisValue)h.Key).ToArray());
        }

        public Task AbandonByErrorAsync(Exception e)
        {
            return db.ListRightPushAsync(_queueName, _redisMessage.RedisValue);
        }

        public Task DeadLetterAsync(int deadLetterLimit)
        {
            return db.ListRightPushAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName), _redisMessage.RedisValue);
        }

        public Task<T> GetMessageAsync()
        {
            return Task.FromResult(_redisMessage.Message);
        }
    }
}