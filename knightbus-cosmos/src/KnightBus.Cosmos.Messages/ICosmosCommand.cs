using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Cosmos.Messages;

public interface ICosmosCommand : ICommand
{
}

public interface ICosmosEvent : IEvent
{
    //int FailedAttempts => 0;
}

public class InternalCosmosMessage<T> where T : IMessage 
{
    public string id { get;  }= Guid.NewGuid().ToString();
    public string Topic { get; }
    public T CosmosEvent { get; }

    public int DeliveryCount { get; set; } = 0;
        
    public InternalCosmosMessage(T CosmosEvent)
    {
        this.Topic = AutoMessageMapper.GetQueueName<T>();
        this.CosmosEvent = CosmosEvent;
    }
    
}
