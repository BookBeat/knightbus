using System.Collections.Generic;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisMessage<T> where T : class, IRedisMessage
    {
        public T Message { get; }
        public string HashKey { get; }
        public string ExpirationKey { get; }
        public RedisValue RedisValue { get; }
        public IDictionary<string, string> HashEntries { get; }

        public RedisMessage(RedisValue redisValue, string id, T message, HashEntry[] hashEntries, string queueName)
        {
            RedisValue = redisValue;
            HashKey = RedisQueueConventions.GetMessageHashKey(queueName, id);
            ExpirationKey = RedisQueueConventions.GetMessageExpirationKey(queueName, id);
            Message = message;
            HashEntries = hashEntries.ToStringDictionary();
        }
    }
}