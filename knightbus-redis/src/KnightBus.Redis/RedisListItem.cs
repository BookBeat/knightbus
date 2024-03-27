using KnightBus.Messages;

namespace KnightBus.Redis;

public class RedisListItem<T> where T : IMessage
{
    public RedisListItem(string id, T body)
    {
        Id = id;
        Body = body;
    }

    public string Id { get; set; }
    public T Body { get; set; }
}
