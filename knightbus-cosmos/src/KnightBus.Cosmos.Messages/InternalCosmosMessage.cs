using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Cosmos.Messages;

public class InternalCosmosMessage<T>(T messageBody, TimeSpan _ttl)
    where T : IMessage
{
    public string id { get; set; } = Guid.NewGuid().ToString();

    public int ttl { get; set; } = (int)_ttl.TotalSeconds;
    public T Message { get; set; } = messageBody;
    public int DeliveryCount { get; set; } = 0;

    public DeadLetterCosmosMessage<T> ToDeadLetterMessage(string topic, string subscription)
    {
        return new DeadLetterCosmosMessage<T>(Message, subscription)
        {
            TTLOfDeadletteredMessage = ttl,
            id = id,
            DeliveryCount = DeliveryCount,
        };
    }
}

public class DeadLetterCosmosMessage<T>(T Message, string Subscription)
    where T : IMessage
{
    public string id { get; set; } = Guid.NewGuid().ToString();

    public int TTLOfDeadletteredMessage { get; set; }
    public T Message { get; set; } = Message;
    public int DeliveryCount { get; set; } = 0;
    public string Subscription { get; set; } = Subscription;

    public InternalCosmosMessage<T> ToInternalCosmosMessage()
    {
        return new InternalCosmosMessage<T>(Message, TimeSpan.FromSeconds(TTLOfDeadletteredMessage))
        {
            id = id,
            DeliveryCount = DeliveryCount,
        };
    }
}
