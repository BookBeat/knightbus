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
    public Container TopicQueue { get; private set; }
    public Container PersonalQueue { get; private set; }
    public Container Lease { get; private set; }
    private Container DeadLetterQueue { get; set; }
    
    public IEventSubscription? _subscription { get; private set; }
    
    public CosmosQueueClient(ICosmosConfiguration cosmosConfiguration, IEventSubscription? subscription)
    {
        _cosmosConfiguration = cosmosConfiguration;
        _subscription = subscription;
    }

    // subscription client

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //Create cosmos client
        Client = new CosmosClient(_cosmosConfiguration.ConnectionString);
        
        //Get database, create if it does not exist
        DatabaseResponse databaseResponse = await Client.CreateDatabaseIfNotExistsAsync(_cosmosConfiguration.Database, 1000, null, cancellationToken);
        CheckHttpResponse(databaseResponse.StatusCode, _cosmosConfiguration.Database);
        Database = databaseResponse.Database;
        
        Lease = await CreateContainerAsync(_cosmosConfiguration.LeaseContainer, cancellationToken);
        
        if (typeof(ICosmosEvent).IsAssignableFrom(typeof(T)))
        {
            await SetupEventAsync(cancellationToken);
        }
        else if (typeof(ICosmosCommand).IsAssignableFrom(typeof(T)))
        {
            await SetupCommandAsync(cancellationToken);
        }
    }

    private async Task SetupEventAsync(CancellationToken cancellationToken)
    {
        TopicQueue = await CreateContainerAsync(AutoMessageMapper.GetQueueName<T>(), cancellationToken);

        PersonalQueue = await CreateContainerAsync(AutoMessageMapper.GetQueueName<T>() + ": " + _subscription.Name, cancellationToken);
        
        DeadLetterQueue = await CreateContainerAsync(AutoMessageMapper.GetQueueName<T>() + ": " + _subscription.Name + " - DL", cancellationToken, -1 ); //Deadlettered items should never expire
    }
    
    private async Task SetupCommandAsync(CancellationToken cancellationToken){
        
        PersonalQueue = await CreateContainerAsync(AutoMessageMapper.GetQueueName<T>(), cancellationToken);
        
        DeadLetterQueue = await CreateContainerAsync(AutoMessageMapper.GetQueueName<T>() + " - DL", cancellationToken, -1 ); //Deadlettered items should never expire
    }

    private async Task<Container> CreateContainerAsync(string id, CancellationToken cancellationToken, int? TTL = null)
    {
        TTL ??= (int)_cosmosConfiguration.DefaultTimeToLive.TotalSeconds; //Get configured TTL if not specified
        const string partitionKeyPath = "/id";
        
        //Get container
        //Update it if it exists and does not match expectations
        try
        {
            Container container = Database.GetContainer(id);
            ContainerProperties properties = (await container.ReadContainerAsync(cancellationToken: cancellationToken)).Resource;

            if (properties.PartitionKeyPath == partitionKeyPath && properties.DefaultTimeToLive == TTL)
            {
                return container;
            }

            properties.PartitionKeyPath = partitionKeyPath;
            properties.DefaultTimeToLive = TTL;
                
            await container.ReplaceContainerAsync(properties, cancellationToken: cancellationToken); //Update container properties, does not loose data

            return container;
        }
        catch
        {
            //Create container if it does not exist
            var response = await Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id, "/id") {DefaultTimeToLive = TTL}, cancellationToken: cancellationToken);
            CheckHttpResponse(response.StatusCode, id);
            return response.Container;
        }
    } 
    
    private static void CheckHttpResponse(HttpStatusCode statusCode, string id)
    {
        if(statusCode != HttpStatusCode.OK && statusCode != HttpStatusCode.Created) 
            throw new Exception($"Unexpected http response when creating {id} : {statusCode}");
    }
    
    public async Task CompleteAsync(InternalCosmosMessage<T> message)
    {
        await RemoveItemAsync(message, PersonalQueue);
    }

    public async Task AbandonByErrorAsync(InternalCosmosMessage<T> message)
    {
        //Update item
         ItemResponse<InternalCosmosMessage<T>> response = await PersonalQueue.PatchItemAsync<InternalCosmosMessage<T>>(

            id: message.id,
            partitionKey: new PartitionKey(message.id),
            patchOperations:
            [
                PatchOperation.Increment("/DeliveryCount", 1)
            ]
        );
    }

    public async Task DeadLetterAsync(InternalCosmosMessage<T> message)
    {
        await DeadLetterQueue.CreateItemAsync(message, new PartitionKey(message.id));
        await RemoveItemAsync(message, PersonalQueue);
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
