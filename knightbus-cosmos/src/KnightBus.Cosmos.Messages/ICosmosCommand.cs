using KnightBus.Messages;

namespace KnightBus.Cosmos.Messages;

public interface ICosmosCommand : ICommand
{
}

public interface ICosmosEvent : IEvent
{
    public string id { get;  }
    public string Topic { get; }
    
    public string MessageBody { get; }
}
