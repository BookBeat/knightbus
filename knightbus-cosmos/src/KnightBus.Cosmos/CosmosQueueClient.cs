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
        //var deadLetterResponse = await _database.CreateContainerIfNotExistsAsync(
        //    new ContainerProperties(_cosmosConfiguration.deadLetterContainer, "/id"), cancellationToken: cancellationToken);
        //CheckHttpResponse(deadLetterResponse.StatusCode, _cosmosConfiguration.deadLetterContainer);
        //Container = deadLetterResponse.Container;

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
        //Remove item from container
        // Multiple change feeds per subscriber: Remove from processing queue
        //var response = await _container.DeleteItemAsync
    }

    public async Task AbandonByErrorAsync(InternalCosmosMessage<T> message)
    {
        Console.WriteLine($"AbandonByError: {message.DeliveryCount}");
        //Update item
        /*
         ItemResponse<InternalCosmosMessage<T>> response = await Container.PatchItemAsync<InternalCosmosMessage<T>>(
         
            id: message.id,
            partitionKey: new PartitionKey(message.Topic),
            patchOperations:
            [
                PatchOperation.Increment("/FailedAttempts", 1)
            ]
        );
        */
        

        
        // TODO to support multiple change feeds per subscriber: Remove from processing container
    }

    public async Task DeadLetterAsync(InternalCosmosMessage<T> message)
    {
        Console.WriteLine("Message should be deadLettered");
        //Remove from container
        //Add to deadletterContainer
    }
    
}
