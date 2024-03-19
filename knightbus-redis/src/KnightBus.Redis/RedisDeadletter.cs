using System.Collections.Generic;
using KnightBus.Redis.Messages;

namespace KnightBus.Redis;

public class RedisDeadletter<T> where T : IRedisMessage
{
    public RedisListItem<T> Message { get; internal set; }

    public IDictionary<string, string> HashEntries { get; internal set; }
}
