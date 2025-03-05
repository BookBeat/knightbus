using KnightBus.Messages;

namespace KnightBus.Cosmos.Messages;

public interface ICosmosCommand : ICommand
{
}

public interface ICosmosEvent : IEvent
{
    string id { get;  }
    string topic { get; }
    
    string messageBody { get; }
}
