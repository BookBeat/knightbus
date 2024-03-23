using System.Collections.Generic;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis;

internal class RedisMessage<T> where T : class, IRedisMessage
{
    public T Message { get; }
    public string HashKey { get; }
    public RedisValue RedisValue { get; }
    public IDictionary<string, string> HashEntries { get; }

    public RedisMessage(RedisValue redisValue, string id, T message, HashEntry[] hashEntries, string queueName)
    {
        RedisValue = redisValue;
        HashKey = RedisQueueConventions.GetMessageHashKey(queueName, id);
        Message = message;
        HashEntries = hashEntries.ToStringDictionary();
    }
}
