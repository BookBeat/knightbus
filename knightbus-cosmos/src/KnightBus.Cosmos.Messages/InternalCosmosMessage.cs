using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Cosmos.Messages;

public class InternalCosmosMessage<T>(T messageBody)
    where T : IMessage
{
    public string id { get; set; } = Guid.NewGuid().ToString();
    public T Message { get; set;  } = messageBody;
    public int DeliveryCount { get; set; } = 0;
    
}
