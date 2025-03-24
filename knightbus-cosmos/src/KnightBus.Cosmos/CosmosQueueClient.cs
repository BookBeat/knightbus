using System.Net;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public class CosmosQueueClient<T> where T : class, IMessage
{
    private readonly ICosmosConfiguration _cosmosConfiguration;
    private CosmosClient _client;
    private Database _database;
    public Container Container;
    private Container _deadLetterContainer;
    
    public CosmosQueueClient(ICosmosConfiguration cosmosConfiguration)
    {
        _cosmosConfiguration = cosmosConfiguration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        
        //Create cosmos client
        _client = new CosmosClient(_cosmosConfiguration.ConnectionString);
        
        //Get database, create if it does not exist
        DatabaseResponse databaseResponse = await _client.CreateDatabaseIfNotExistsAsync(_cosmosConfiguration.Database, 400, null, cancellationToken);
        CheckHttpResponse(databaseResponse.StatusCode, _cosmosConfiguration.Database);
        _database = databaseResponse.Database;
        
        //Get container, create if it does not exist
        var containerResponse = await _database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_cosmosConfiguration.Container, "/Topic") {DefaultTimeToLive = (int)_cosmosConfiguration.DefaultTimeToLive.TotalSeconds}, cancellationToken: cancellationToken);
        CheckHttpResponse(containerResponse.StatusCode, _cosmosConfiguration.Container);
        Container = containerResponse.Container;
        
        //Get deadLetterContainer, create if it does not exist
        var deadLetterResponse = await _database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_cosmosConfiguration.deadLetterContainer, "/Topic"), cancellationToken: cancellationToken);
        CheckHttpResponse(deadLetterResponse.StatusCode, _cosmosConfiguration.deadLetterContainer);
        _deadLetterContainer = deadLetterResponse.Container;

    }
    
    private static void CheckHttpResponse(HttpStatusCode statusCode, string id)
    {
        switch (statusCode)
        {
            case HttpStatusCode.Created:
                Console.WriteLine($" {id} created");
                break;
            case HttpStatusCode.OK:
                Console.WriteLine($"{id} already exists");
                break;
            default:
                throw new Exception($"Unexpected http response when creating {id} : {statusCode}");
        }
    }
    
    
    public async Task CompleteAsync(InternalCosmosMessage<T> message)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async Task AbandonByErrorAsync(InternalCosmosMessage<T> message)
    {
        //Update item
        try
        {
             ItemResponse<InternalCosmosMessage<T>> response = await Container.PatchItemAsync<InternalCosmosMessage<T>>(

                id: message.id,
                partitionKey: new PartitionKey(message.Topic),
                patchOperations:
                [
                    PatchOperation.Add("/teststring", "test"),
                    PatchOperation.Increment("/DeliveryCount", 1)
                ]
            );
        }
        catch (CosmosException ex)
        {
            //TODO: Should be made into log error
            Console.WriteLine($"Cosmos error {ex.StatusCode}");
        }
    }

    public async Task DeadLetterAsync(InternalCosmosMessage<T> message)
    {
        await AddItemAsync(message, _deadLetterContainer);
        await RemoveItemAsync(message, Container);
    }


    public async Task AddItemAsync(InternalCosmosMessage<T> message, Container container)
    {
        await container.CreateItemAsync(message, new PartitionKey(message.Topic));
    }
    
    public async Task RemoveItemAsync(InternalCosmosMessage<T> message, Container container)
    {
        await container.DeleteItemAsync<InternalCosmosMessage<T>>(message.id, new PartitionKey(message.Topic));
    }
}
