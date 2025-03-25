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

public class DeadLetterCosmosMessage<T>(string id, T message, string subscription)
    where T : IMessage
{
    public string Subscription { get; set; } = subscription;
    public string Topic { get; set; } = AutoMessageMapper.GetQueueName<T>();
    public string id { get; set; } = id;
    public T message { get; set; } = message;
}
