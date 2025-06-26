using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public class CosmosMessageStateHandler<T>(
    CosmosQueueClient<T> cosmosQueueClient,
    InternalCosmosMessage<T> message,
    int deadLetterDeliveryLimit,
    IDependencyInjection messageScope
) : IMessageStateHandler<T>
    where T : class, IMessage
{
    public int DeliveryCount => message.DeliveryCount;
    public int DeadLetterDeliveryLimit { get; } = deadLetterDeliveryLimit;
    public IDictionary<string, string>? MessageProperties => null; //TODO Should implement?

    public Task CompleteAsync()
    {
        return Task.CompletedTask;
    }

    public Task AbandonByErrorAsync(Exception e)
    {
        return cosmosQueueClient.AbandonByErrorAsync(message);
    }

    public Task DeadLetterAsync(int deadLetterLimit)
    {
        return cosmosQueueClient.DeadLetterAsync(message);
    }

    public T GetMessage()
    {
        return message.Message;
    }

    public Task ReplyAsync<TReply>(TReply reply)
    {
        throw new NotImplementedException();
    }

    public IDependencyInjection MessageScope { get; set; } = messageScope;
}
