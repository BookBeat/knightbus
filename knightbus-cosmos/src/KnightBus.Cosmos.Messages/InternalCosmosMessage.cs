using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Cosmos.Messages;

public class InternalCosmosMessage<T>(T messageBody)
    where T : IMessage
{
    public string id { get; set; } = Guid.NewGuid().ToString();
    public T Message { get; set; } = messageBody;
    public int DeliveryCount { get; set; } = 0;

    public DeadLetterCosmosMessage<T> ToDeadLetterMessage(string topic, string subscription)
    {
        return new DeadLetterCosmosMessage<T>(Message, subscription)
        {
            id = id,
            DeliveryCount = DeliveryCount,
        };
    }
}

public class DeadLetterCosmosMessage<T>(T Message, string Subscription)
    where T : IMessage
{
    public string id { get; set; } = Guid.NewGuid().ToString();
    public T Message { get; set; } = Message;
    public int DeliveryCount { get; set; } = 0;
    public string Subscription { get; set; } = Subscription;

    public InternalCosmosMessage<T> ToInternalCosmosMessage()
    {
        return new InternalCosmosMessage<T>(Message) { id = id, DeliveryCount = DeliveryCount };
    }
}
