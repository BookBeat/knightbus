using System.Collections.Generic;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisMessage<T> where T : class, IRedisMessage
    {
        private readonly string _queueName;
        public string HashKey => RedisQueueConventions.GetMessageHashKey(_queueName, Message.Id);
        public string ExpirationKey => RedisQueueConventions.GetMessageExpirationKey(_queueName, Message.Id);
        public T Message { get; }
        public RedisValue RedisValue { get; }
        public IDictionary<string, string> HashEntries { get; }

        public RedisMessage(RedisValue redisValue, T message, HashEntry[] hashEntries, string queueName)
        {
            _queueName = queueName;
            RedisValue = redisValue;
            Message = message;
            HashEntries = hashEntries.ToStringDictionary();
        }
    }
}