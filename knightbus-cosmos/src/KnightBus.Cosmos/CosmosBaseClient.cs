using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using System.Runtime.CompilerServices;

namespace KnightBus.Cosmos;

public class CosmosBaseClient<T> where T : class, IMessage
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _container;
    private readonly IMessageSerializer _serializer;
    private readonly string _queueName;

    //Constructor
    public CosmosBaseClient(CosmosClient cosmosClient, string databaseName, string containerName,
        IMessageSerializer serializer)
    {
        _cosmosClient = cosmosClient;
        _container = _cosmosClient.GetContainer(databaseName, containerName);
        _serializer = serializer;
    }

    //Gets all messages in changeFeed?
    public async IAsyncEnumerable<CosmosMessage<T>> GetMessagesAsync(int count, int visibilityTimeout,
        [EnumeratorCancellation] CancellationToken ct)
    {
        throw new NotImplementedException("Cosmos GetMessagesAsync not implemented");
    }

    //Marks task as complete
    public async Task CompleteAsync(CosmosMessage<T> message)
    {
        throw new NotImplementedException("Cosmos completeAsync not implemented");
    }

    //These three are not necessary for mvp
    public async Task AbandonByErrorAsync(CosmosMessage<T> message, Exception exception)
    {
        throw new NotImplementedException("Cosmos AbandonByErrorAsync not implemented");
    }

    public async Task DeadLetterMessageAsync(CosmosMessage<T> message)
    {
        throw new NotImplementedException("Cosmos DeadLetterAsync not implemented");
    }

    public async IAsyncEnumerable<CosmosMessage<T>> PeekDeadLetterMessagesAsync(int count,
        [EnumeratorCancellation] CancellationToken ct)
    {
        throw new NotImplementedException("Cosmos PeekDeadLetterMessagesAsync not implemented");
    }

}
