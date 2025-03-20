using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Cosmos.Messages;

public class InternalCosmosMessage<T>(T cosmosEvent)
    where T : IMessage
{
    public string id { get;  }= Guid.NewGuid().ToString();
    public string Topic { get; } = AutoMessageMapper.GetQueueName<T>();
    public T CosmosEvent { get; } = cosmosEvent;

    public int DeliveryCount { get; set; } = 0;
    
    public Dictionary<string, string> Properties { get; init; } = new();
}
