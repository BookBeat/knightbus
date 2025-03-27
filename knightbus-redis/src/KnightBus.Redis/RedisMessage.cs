using System;
using System.Collections.Generic;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis;

public class RedisMessage<T>
    where T : class, IMessage
{
    public string Id { get; }
    public T Message { get; }
    public string HashKey { get; }
    public RedisValue RedisValue { get; }
    public IDictionary<string, string> HashEntries { get; }

    public RedisMessage(
        RedisValue redisValue,
        string id,
        T message,
        HashEntry[] hashEntries,
        string queueName
    )
    {
        Id = id;
        RedisValue = redisValue;
        HashKey = RedisQueueConventions.GetMessageHashKey(queueName, id);
        Message = message;
        HashEntries = hashEntries.ToStringDictionary();
    }

    public DateTimeOffset LastProcessed =>
        HashEntries.TryGetValue(RedisHashKeys.LastProcessed, out var value)
            ? DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(value))
            : DateTimeOffset.MinValue;

    public string Error =>
        HashEntries.TryGetValue(RedisHashKeys.Errors, out var value) ? value : string.Empty;

    public int DeliveryCount =>
        HashEntries.TryGetValue(RedisHashKeys.DeliveryCount, out var value) ? int.Parse(value) : 0;
}
