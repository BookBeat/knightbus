using System.Collections.ObjectModel;
using System.Data;
using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public class CosmosSubscriptionChannelReceiver<T> : IChannelReceiver where T : class, ICosmosEvent
{
    private IProcessingSettings settings;
    private IMessageSerializer _serializer;
    private IEventSubscription _subscription;
    private IHostConfiguration _hostConfiguration;
    private IMessageProcessor _processor;
    private ICosmosConfiguration _cosmosConfiguration;
    private CosmosClient _client;
    private Database _database;
    private Container _container;
    private static T _messageType; //Used only to extract name of message type
    
    public CosmosSubscriptionChannelReceiver(
        IProcessingSettings processorSettings,
        IMessageSerializer serializer,
        IEventSubscription subscription, //Currently not used
        IHostConfiguration config,
        IMessageProcessor processor,
        ICosmosConfiguration cosmosConfiguration
    )
    {
        settings = processorSettings;
        _serializer = serializer;
        _subscription = subscription;
        _hostConfiguration = config;
        _processor = processor;
        _cosmosConfiguration = cosmosConfiguration;

    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //Create cosmos client
        _client = new CosmosClient(_cosmosConfiguration.ConnectionString);
        
        //Get database, create if it does not exist
        DatabaseResponse databaseResponse = await _client.CreateDatabaseIfNotExistsAsync(_cosmosConfiguration.Database, 400, null, cancellationToken);
        CheckResponse(databaseResponse, _cosmosConfiguration.Database);
        
        
        _database = databaseResponse.Database;
        //Get container, create if it does not exist
        var response = await _database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_cosmosConfiguration.Container, "/Topic") {DefaultTimeToLive = (int)_cosmosConfiguration.DefaultTimeToLive.TotalSeconds}, cancellationToken: cancellationToken);
        CheckResponse(response, _cosmosConfiguration.Container);
        _container = response.Container;


        StartPullModelChangeFeed(cancellationToken);

    }
    
    
    //Start pull model change feed
    private async Task StartPullModelChangeFeed(CancellationToken cancellationToken)
    {        
        var topic = AutoMessageMapper.GetQueueName<T>();

        //Start pull model
        var iteratorForPartitionKey = _container.GetChangeFeedIterator<InternalCosmosMessage<T>>(
            ChangeFeedStartFrom.Beginning(FeedRange.FromPartitionKey(new PartitionKey(topic))), ChangeFeedMode.LatestVersion);
        
        Console.WriteLine($"starting pull model change feed on topic : {topic}");
        // Function that called this function with await should be allowed to continue when this function reaches here

        while (iteratorForPartitionKey.HasMoreResults) // && !cancellationToken.IsCancellationRequested) // change feed stops when program reaches end
        {
            FeedResponse<InternalCosmosMessage<T>> response = await iteratorForPartitionKey.ReadNextAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                // 1 RU used if no changes have been made
                await Task.Delay(_cosmosConfiguration.PollingDelay, cancellationToken);
            }
            else
            {
                foreach (InternalCosmosMessage<T> message in response)
                {
                    await ProcessMessageAsync(message, cancellationToken);
                    
                }
            }
        }
    }

    private async Task ProcessMessageAsync(InternalCosmosMessage<T> message, CancellationToken cancellationToken)
    {
        var messageStateHandler = new CosmosMessageStateHandler<T>(_client, message, settings.DeadLetterDeliveryLimit, _serializer, _hostConfiguration.DependencyInjection);
        await _processor.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
    }

    private static void CheckResponse(object response, string id)
    {
        switch (response)
        {
            case DatabaseResponse databaseResponse:
                CheckHttpResponse(databaseResponse.StatusCode , "database", id);
                break;
            case ContainerResponse containerResponse:
                CheckHttpResponse(containerResponse.StatusCode, "container", id);
                break;
            default:
                throw new ArgumentException(
                    "Invalid response type, only databaseResponse & containerResponse are supported");
        }
    }
    private static void CheckHttpResponse(HttpStatusCode statusCode, string type, string id)
    {
        switch (statusCode)
        {
            case HttpStatusCode.Created:
                Console.WriteLine($"{type}: {id} created");
                break;
            case HttpStatusCode.OK:
                Console.WriteLine($"{type}: {id} already exists");
                break;
            default:
                throw new Exception($"Unexpected http response code when creating {type} {id} : {statusCode}");
        }
    }
    

    //Create Container if it does not exist, old containers with incompatible settings to the new one crash server
    private static async Task<Container> CreateContainerIfNotExistsOrIncompatibleAsync(CosmosClient client, Database db, string containerId, string partitionKey, int defaultTTL)
    {
        //Get container
        
        //Get metadata to check compatibility with new container
        
        //Create a new container
        var response = await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties(containerId, partitionKey) {DefaultTimeToLive = defaultTTL});
        CheckResponse(response, containerId);

        return response.Container;
    }
    
    public IProcessingSettings Settings { get; set; }
}
