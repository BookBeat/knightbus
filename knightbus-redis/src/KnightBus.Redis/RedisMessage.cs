using System;
using System.Collections.Generic;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis;

public class RedisMessage<T> where T : class, IRedisMessage
{
    public string Id { get; }
    public T Message { get; }
    public string HashKey { get; }
    public RedisValue RedisValue { get; }
    public IDictionary<string, string> HashEntries { get; }

    public RedisMessage(RedisValue redisValue, string id, T message, HashEntry[] hashEntries, string queueName)
    {
        Id = id;
        RedisValue = redisValue;
        HashKey = RedisQueueConventions.GetMessageHashKey(queueName, id);
        Message = message;
        HashEntries = hashEntries.ToStringDictionary();
    }

    public DateTimeOffset LastProcessed =>
        !HashEntries.ContainsKey(RedisHashKeys.LastProcessed) ?
            DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(HashEntries[RedisHashKeys.LastProcessed])) :
            DateTimeOffset.MinValue;

    public string Error =>
        !HashEntries.ContainsKey(RedisHashKeys.Errors) ?
            HashEntries[RedisHashKeys.LastProcessed] :
            string.Empty;

    public int DeliveryCount =>
        !HashEntries.ContainsKey(RedisHashKeys.DeliveryCount) ?
            int.Parse(HashEntries[RedisHashKeys.LastProcessed]) : 0;
}
