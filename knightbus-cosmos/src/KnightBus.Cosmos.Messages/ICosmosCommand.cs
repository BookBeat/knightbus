using KnightBus.Messages;

namespace KnightBus.Cosmos.Messages;

public interface ICosmosCommand : ICommand
{
}

public interface ICosmosEvent : IEvent
{
    public string Topic { get; }

    //int FailedAttempts => 0;
    
    //string id => Guid.NewGuid().ToString();
}
