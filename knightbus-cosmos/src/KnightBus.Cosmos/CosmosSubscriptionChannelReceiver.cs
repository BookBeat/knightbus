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
        DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(_cosmosConfiguration.Database);
        checkResponse(databaseResponse, _cosmosConfiguration.Database);
        
        Database database = databaseResponse.Database;
        //Get container, create if it does not exist
        Container items = await CreateContainerIfNotExistsOrIncompatibleAsync(client, database, _cosmosConfiguration.Container, "/topic", (int)_cosmosConfiguration.DefaultTimeToLive.TotalSeconds);
        
        Container leaseContainer = await CreateContainerIfNotExistsOrIncompatibleAsync(client, database, "lease", "/id", (int)_cosmosConfiguration.DefaultTimeToLive.TotalSeconds);

         ChangeFeedProcessor changeFeedProcessor = items
            .GetChangeFeedProcessorBuilder<CosmosEvent>(
                processorName: "changeFeed",
                onChangesDelegate: HandleChangesAsync)
            .WithInstanceName("consoleHost")
            .WithLeaseContainer(leaseContainer)
            .WithPollInterval(_cosmosConfiguration.PollingDelay)
            .Build();

         try
         {
             await changeFeedProcessor.StartAsync();
             Console.WriteLine("Change feed processor started.");
         }
         catch
         {
             Console.WriteLine("Failed to start change feed processor");
         }
    }
    

    //Change Feed Handler
    private async Task HandleChangesAsync(
        IReadOnlyCollection<CosmosEvent> changes, 
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Changes: {changes.Count}");
        foreach (var change in changes)
        {
            // Print the message_data received
            Console.WriteLine($"Message {change.id} received with data: {change.messageBody}");
        }
    }
    
    public class CosmosEvent : ICosmosEvent
    {
        public string id { get; }
        public string topic { get; }
        public string? messageBody { get; }

        public CosmosEvent(string id, string topic, string? data = null)
        {
            //Throw exception if either of args are null
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(topic);
            //Assign values
            this.id = id;
            this.topic = topic;
            this.messageBody = data;
        }
    }


    private static void checkResponse(object response, string id)
    {
        switch (response)
        {
            case DatabaseResponse databaseResponse:
                HandleResponse(databaseResponse.StatusCode , "database", id);
                break;
            case ContainerResponse containerResponse:
                HandleResponse(containerResponse.StatusCode, "container", id);
                break;
            default:
                throw new ArgumentException(
                    "Invalid response type, only databaseResponse & containerResponse are supported");
        }
    }
    private static void HandleResponse(HttpStatusCode statusCode, string type, string id)
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
        checkResponse(response, containerId);

        return response.Container;
    }
    
    public IProcessingSettings Settings { get; set; }
}
