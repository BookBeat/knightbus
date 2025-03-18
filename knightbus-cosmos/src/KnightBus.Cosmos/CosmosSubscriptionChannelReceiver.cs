using System.Data;
using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public class CosmosSubscriptionChannelReceiver<T> : IChannelReceiver where T : class, ICosmosEvent
{
    private IProcessingSettings _processorSettings;
    private IMessageSerializer _serializer;
    private IEventSubscription _subscription;
    private IHostConfiguration _configuration;
    private IMessageProcessor _processor;
    private ICosmosConfiguration _cosmosConfiguration;
    
    public CosmosSubscriptionChannelReceiver(
        IProcessingSettings processorSettings,
        IMessageSerializer serializer,
        IEventSubscription subscription,
        IHostConfiguration config,
        IMessageProcessor processor,
        ICosmosConfiguration cosmosConfiguration
    )
    {
        _processorSettings = processorSettings;
        _serializer = serializer;
        _subscription = subscription;
        _configuration = config;
        _processor = processor;
        _cosmosConfiguration = cosmosConfiguration;

    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //Create cosmos client
        CosmosClient client = new CosmosClient(_cosmosConfiguration.ConnectionString);
        
        //Get database, create if it does not exist
        DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_cosmosConfiguration.Database, 400, null, cancellationToken);
        CheckResponse(databaseResponse, _cosmosConfiguration.Database);
        
        Database database = databaseResponse.Database;
        //Get container, create if it does not exist
        Container items = await CreateContainerIfNotExistsOrIncompatibleAsync(client, database, _cosmosConfiguration.Container, "/Topic", (int)_cosmosConfiguration.DefaultTimeToLive.TotalSeconds);

        foreach (string topic in _cosmosConfiguration.Topics)
        {
            Task.Run(() => StartPullModelChangeFeed(items, topic, cancellationToken), cancellationToken);
        }
    }
    
    
    //Start pull model change feed
    private async Task StartPullModelChangeFeed(Container container, string topic, CancellationToken cancellationToken)
    {
        //Start pull model
        var iteratorForPartitionKey = container.GetChangeFeedIterator<T>(
            ChangeFeedStartFrom.Beginning(FeedRange.FromPartitionKey(new PartitionKey(topic))), ChangeFeedMode.LatestVersion);
        
        Console.WriteLine($"starting pull model change feed on topic : {topic}");
        // Function that called this function with await should be allowed to continue when this function reaches here

        while (iteratorForPartitionKey.HasMoreResults && !cancellationToken.IsCancellationRequested)
        {
            FeedResponse<T> response = await iteratorForPartitionKey.ReadNextAsync();

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                //Console.WriteLine($"No new changes");
                await Task.Delay(_cosmosConfiguration.PollingDelay);
            }
            else
            {
                foreach (T message in response)
                {
                    //TODO : Processing behaviour should be passed instead of hard coded
                    Console.WriteLine($"Change in message {message.id} on {topic}");
                }
            }
        }
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
