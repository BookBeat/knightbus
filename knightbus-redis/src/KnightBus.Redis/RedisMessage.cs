using System.Collections.Generic;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisMessage<T> where T : class, IRedisCommand
    {
        private readonly string _queueName;
        public string HashKey => RedisQueueConventions.GetHashKey(Message.Id, _queueName);
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