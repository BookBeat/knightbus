using KnightBus.Redis.Messages;

namespace KnightBus.Redis
{
    internal class RedisListItem<T> where T : IRedisMessage
    {
        public RedisListItem(string id, T body)
        {
            Id = id;
            Body = body;
        }

        public string Id { get; set; }
        public T Body { get; set; }
    }
}
