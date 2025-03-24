using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public class CosmosQueueClient<T> where T : class, IMessage
{
    private readonly ICosmosConfiguration _cosmosConfiguration;
    public CosmosClient Client { get; private set; }
    public Database Database { get; private set; }
    public Container Container { get; private set; }
    
    public Container Lease { get; private set; }
    
    public Container DeadLetterContainer { get; private set; }
    
    public CosmosQueueClient(ICosmosConfiguration cosmosConfiguration)
    {
        _cosmosConfiguration = cosmosConfiguration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        
        //Create cosmos client
        Client = new CosmosClient(_cosmosConfiguration.ConnectionString);
        
        //Get database, create if it does not exist
        DatabaseResponse databaseResponse = await Client.CreateDatabaseIfNotExistsAsync(_cosmosConfiguration.Database, 400, null, cancellationToken);
        CheckHttpResponse(databaseResponse.StatusCode, _cosmosConfiguration.Database);
        Database = databaseResponse.Database;
        
        //Get container, create if it does not exist
        var containerResponse = await Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(AutoMessageMapper.GetQueueName<T>(), "/id") {DefaultTimeToLive = (int)_cosmosConfiguration.DefaultTimeToLive.TotalSeconds}, cancellationToken: cancellationToken);
        CheckHttpResponse(containerResponse.StatusCode, AutoMessageMapper.GetQueueName<T>());
        Container = containerResponse.Container;
        
        //Get lease container, create if it does not exist
        var leaseResponse = await Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_cosmosConfiguration.LeaseContainer, "/id") {DefaultTimeToLive = (int)_cosmosConfiguration.DefaultTimeToLive.TotalSeconds}, cancellationToken: cancellationToken);
        CheckHttpResponse(containerResponse.StatusCode, _cosmosConfiguration.LeaseContainer);
        Lease = leaseResponse.Container;
        
        //Get DeadLetterContainer, create if it does not exist
        var deadLetterResponse = await Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_cosmosConfiguration.DeadLetterContainer, "/id"), cancellationToken: cancellationToken);
        CheckHttpResponse(deadLetterResponse.StatusCode, _cosmosConfiguration.DeadLetterContainer);
        DeadLetterContainer = deadLetterResponse.Container;

    }
    
    private static void CheckHttpResponse(HttpStatusCode statusCode, string id)
    {
        switch (statusCode)
        {
            case HttpStatusCode.Created:
                // Sucessfully created
                break;
            case HttpStatusCode.OK:
                //Already exists
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
                partitionKey: new PartitionKey(message.id),
                patchOperations:
                [
                    PatchOperation.Increment("/DeliveryCount", 1)
                ]
            );
        }
        catch (CosmosException ex)
        {
            //TODO: Make better error handling
            Console.WriteLine($"Cosmos error {ex.StatusCode}");
        }
    }

    public async Task DeadLetterAsync(InternalCosmosMessage<T> message)
    {
        await AddItemAsync(message, DeadLetterContainer);
        await RemoveItemAsync(message, Container);
    }


    public async Task AddItemAsync(InternalCosmosMessage<T> message, Container container)
    {
        await container.CreateItemAsync(message, new PartitionKey(message.id));
    }
    
    public async Task RemoveItemAsync(InternalCosmosMessage<T> message, Container container)
    {
        await container.DeleteItemAsync<InternalCosmosMessage<T>>(message.id, new PartitionKey(message.id));
    }
}
