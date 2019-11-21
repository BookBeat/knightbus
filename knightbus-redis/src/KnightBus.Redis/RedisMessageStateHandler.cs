using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisMessageStateHandler<T> : IMessageStateHandler<T> where T : class, IMessage, IRedisMessage
    {
        private readonly IDatabase _db;
        private readonly RedisMessage<T> _redisMessage;
        private readonly string _queueName;


        public RedisMessageStateHandler(IConnectionMultiplexer connection, RedisConfiguration configuration, RedisMessage<T> redisMessage, int deadLetterDeliveryLimit, string queueName)
        {
            _db = connection.GetDatabase(configuration.DatabaseId);
            _redisMessage = redisMessage;
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            _queueName = queueName;
        }

        public int DeliveryCount => int.Parse(_redisMessage.HashEntries[RedisHashKeys.DeliveryCount]);
        public int DeadLetterDeliveryLimit { get; }

        public IDictionary<string, string> MessageProperties => _redisMessage.HashEntries;
        public Task CompleteAsync()
        {
            return Task.WhenAll(
                _db.KeyDeleteAsync(new RedisKey[] { _redisMessage.HashKey, _redisMessage.ExpirationKey }),
                _db.ListRemoveAsync(RedisQueueConventions.GetProcessingQueueName(_queueName), _redisMessage.RedisValue, -1));
        }

        public Task AbandonByErrorAsync(Exception e)
        {
            return Task.WhenAll(
                _db.HashSetAsync(_redisMessage.HashKey, RedisHashKeys.Errors, $"{e.Message}\n{e.StackTrace}"),
                _db.ListLeftPushAsync(_queueName, _redisMessage.RedisValue),
                _db.ListRemoveAsync(RedisQueueConventions.GetProcessingQueueName(_queueName), _redisMessage.RedisValue, -1)
                );
        }

        public Task DeadLetterAsync(int deadLetterLimit)
        {
            return Task.WhenAll(
                _db.HashSetAsync(_redisMessage.HashKey, "MaxDeliveryCountExceeded", $"DeliveryCount exceeded limit of {DeadLetterDeliveryLimit}"),
                _db.ListLeftPushAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName), _redisMessage.RedisValue),
                _db.ListRemoveAsync(RedisQueueConventions.GetProcessingQueueName(_queueName), _redisMessage.RedisValue, -1));
        }

        public Task<T> GetMessageAsync()
        {
            return Task.FromResult(_redisMessage.Message);
        }
    }
}