using System;
using System.Collections.Generic;
using KnightBus.Redis.Messages;

namespace KnightBus.Redis;

public class RedisDeadletter<T> where T : IRedisMessage
{
    public RedisListItem<T> Message { get; internal set; }

    public IDictionary<string, string> HashEntries { get; internal set; }

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
