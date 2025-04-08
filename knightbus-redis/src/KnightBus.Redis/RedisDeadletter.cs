using System;
using System.Collections.Generic;
using KnightBus.Messages;

namespace KnightBus.Redis;

public class RedisDeadletter<T>
    where T : IMessage
{
    public RedisListItem<T> Message { get; internal set; }

    public IDictionary<string, string> HashEntries { get; internal set; }

    public DateTimeOffset LastProcessed =>
        HashEntries.TryGetValue(RedisHashKeys.LastProcessed, out var value)
            ? DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(value))
            : DateTimeOffset.MinValue;

    public string Error =>
        HashEntries.TryGetValue(RedisHashKeys.Errors, out var value) ? value : string.Empty;

    public int DeliveryCount =>
        HashEntries.TryGetValue(RedisHashKeys.DeliveryCount, out var value) ? int.Parse(value) : 0;
}
