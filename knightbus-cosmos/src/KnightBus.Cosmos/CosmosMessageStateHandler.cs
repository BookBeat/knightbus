using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public class CosmosMessageStateHandler<T> : IMessageStateHandler<T>
    where T : class, IMessage //ICosmosEvent prob shouldn't be needed
{
    private readonly CosmosQueueClient<T> _cosmosQueueClient;
    private readonly InternalCosmosMessage<T> _internalMessage;

    public CosmosMessageStateHandler(
        CosmosQueueClient<T> cosmosQueueClient,
        InternalCosmosMessage<T> message,
        int deadLetterDeliveryLimit,
        IDependencyInjection messageScope
    )
    {
        _cosmosQueueClient = cosmosQueueClient;
        _internalMessage = message;
        DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
        MessageScope = messageScope;
    }

    public int DeliveryCount => _internalMessage.DeliveryCount;
    public int DeadLetterDeliveryLimit { get; }
    public IDictionary<string, string>? MessageProperties => null; // Not implemented

    public Task CompleteAsync()
    {
        return _cosmosQueueClient.CompleteAsync(_internalMessage);
    }

    public Task AbandonByErrorAsync(Exception e)
    {
        return _cosmosQueueClient.AbandonByErrorAsync(_internalMessage);
    }

    public Task DeadLetterAsync(int deadLetterLimit)
    {
        return _cosmosQueueClient.DeadLetterAsync(_internalMessage);
    }

    public T GetMessage()
    {
        return _internalMessage.Message;
    }

    public Task ReplyAsync<TReply>(TReply reply)
    {
        throw new NotImplementedException();
    }

    public IDependencyInjection? MessageScope { get; set; }
}
