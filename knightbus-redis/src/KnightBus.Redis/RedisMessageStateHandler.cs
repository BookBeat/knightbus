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
        private readonly RedisQueueClient<T> _queueClient;
        private readonly RedisMessage<T> _redisMessage;

        public RedisMessageStateHandler(IConnectionMultiplexer connection, RedisConfiguration configuration, RedisMessage<T> redisMessage, int deadLetterDeliveryLimit, IDependencyInjection messageScope)
        {
            _redisMessage = redisMessage;
            _queueClient = new RedisQueueClient<T>(connection.GetDatabase(configuration.DatabaseId));
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            MessageScope = messageScope;
        }

        public int DeliveryCount => int.Parse(_redisMessage.HashEntries[RedisHashKeys.DeliveryCount]);
        public int DeadLetterDeliveryLimit { get; }

        public IDictionary<string, string> MessageProperties => _redisMessage.HashEntries;
        public Task CompleteAsync()
        {
            return _queueClient.CompleteMessageAsync(_redisMessage);
        }

        public Task AbandonByErrorAsync(Exception e)
        {
            return _queueClient.AbandonMessageByErrorAsync(_redisMessage, e);
        }

        public Task DeadLetterAsync(int deadLetterLimit)
        {
            return _queueClient.DeadLetterMessageAsync(_redisMessage, DeadLetterDeliveryLimit);
        }

        public Task<T> GetMessageAsync()
        {
            return Task.FromResult(_redisMessage.Message);
        }

        public IDependencyInjection MessageScope { get; set; }
    }
}