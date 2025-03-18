using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;


public class CosmosMessageStateHandler<T> :
    IMessageStateHandler<T> where T : class, IMessage
{
    private readonly CosmosClient? _cosmosClient;
    private readonly CosmosMessage<T>? _message;

    public CosmosMessageStateHandler(
        CosmosClient cosmosClient,
        CosmosMessage<T> message,
        int deadLetterDeliveryLimit,
        IMessageSerializer serializer,
        IDependencyInjection messageScope)
    {
        _cosmosClient = cosmosClient;
        _message = message;
        DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
        MessageScope = messageScope;
    }

    public CosmosMessageStateHandler()
    {
    }

    public int DeliveryCount => _message.ReadCount;
    public int DeadLetterDeliveryLimit { get; }
    public IDictionary<string, string> MessageProperties => _message.Properties;

    public Task CompleteAsync()
    {
       throw new NotImplementedException();
    }

    public Task AbandonByErrorAsync(Exception e)
    {
        throw new NotImplementedException();
    }

    public Task DeadLetterAsync(int deadLetterLimit)
    {
        throw new NotImplementedException();
    }

    public T GetMessage()
    {
        return _message.Message;
    }

    public Task ReplyAsync<TReply>(TReply reply)
    {
        throw new NotImplementedException();
    }

    public IDependencyInjection? MessageScope { get; set; }
}
