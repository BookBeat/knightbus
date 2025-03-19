using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;


public class CosmosMessageStateHandler<T> :
    IMessageStateHandler<T> where T : class, IMessage //ICosmosEvent prob shouldn't be needed
{
    private readonly CosmosClient? _cosmosClient;
    private readonly InternalCosmosMessage<T> _message;

    public CosmosMessageStateHandler(
        CosmosClient cosmosClient,
        InternalCosmosMessage<T> message,
        int deadLetterDeliveryLimit,
        IMessageSerializer serializer,
        IDependencyInjection messageScope)
    {
        _cosmosClient = cosmosClient;
        _message = message;
        DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
        MessageScope = messageScope;
    }

    public int DeliveryCount => _message.DeliveryCount;
    public int DeadLetterDeliveryLimit { get; }
    public IDictionary<string, string> MessageProperties => null ; // Not implemented

    public Task CompleteAsync()
    {
        return Task.CompletedTask;
        //Remove item from processing (and from container?)
    }

    public Task AbandonByErrorAsync(Exception e)
    {
        //Remove item from processing and increment deliveryCount
        return Task.FromException(e);
    }

    public Task DeadLetterAsync(int deadLetterLimit)
    {
        throw new NotImplementedException();
    }

    public T GetMessage()
    {
        return _message.CosmosEvent;
    }

    public Task ReplyAsync<TReply>(TReply reply)
    {
        throw new NotImplementedException();
    }

    public IDependencyInjection? MessageScope { get; set; }
}
