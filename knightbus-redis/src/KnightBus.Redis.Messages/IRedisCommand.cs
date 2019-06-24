using KnightBus.Messages;

namespace KnightBus.Redis.Messages
{
    public interface IRedisMessage:IMessage
    {
        string Id { get; }
    }
    public interface IRedisCommand : ICommand, IRedisMessage
    {
        
    }

    public interface IRedisEvent : IEvent, IRedisMessage
    {
        
    }
}
