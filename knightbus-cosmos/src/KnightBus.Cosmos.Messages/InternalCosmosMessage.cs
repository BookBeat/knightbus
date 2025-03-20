using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Cosmos.Messages;

public class InternalCosmosMessage<T>(T cosmosEvent)
    where T : IMessage
{
    public string id { get; set; }= Guid.NewGuid().ToString();
    public string Topic { get; set; } = AutoMessageMapper.GetQueueName<T>();
    public T CosmosEvent { get; set;  } = cosmosEvent;

    public int DeliveryCount { get; set; } = 0;
    
    public Dictionary<string, string> Properties { get; init; } = new();
}
