using KnightBus.Messages;

namespace KnightBus.Redis.Messages;

public interface IRedisMessage : IMessage
{

}

public interface IRedisCommand : ICommand, IRedisMessage
{

}

public interface IRedisEvent : IEvent, IRedisMessage
{

}
