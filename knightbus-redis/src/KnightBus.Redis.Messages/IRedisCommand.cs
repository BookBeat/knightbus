using KnightBus.Messages;

namespace KnightBus.Redis.Messages
{
    public interface IRedisCommand : ICommand
    {
        string Id { get; }
    }
}
