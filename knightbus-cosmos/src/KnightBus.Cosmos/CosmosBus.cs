using System.Net;
using KnightBus.Cosmos.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;
public interface ICosmosBus
{
    
    //Send single command
    Task SendAsync<T>(T message, CancellationToken ct) where T : ICosmosCommand;
    
    //Send Multiple commands
    Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosCommand;
    
    //Publish one event
    Task PublishAsync<T>(T message, CancellationToken ct) where T : ICosmosEvent;
    
    //Publish multiple events
    Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosEvent;
    
    //Schedule one command
    Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand;
    
    //Schedule multiple commands
    Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand;
}

public class CosmosBus : ICosmosBus
{
    public CosmosClient _client;
    //Constructor
    public CosmosBus(ICosmosConfiguration config)
    {
        string? connectionString = config.ConnectionString;
        ArgumentNullException.ThrowIfNull(connectionString);
        //Instantiate CosmosClient
        _client = new CosmosClient(connectionString,
            new CosmosClientOptions() { ApplicationName = "Sender" });
    }

    public void cleanUp()
    {
        _client.Dispose();
    }
    
    //Send a single command
    public Task SendAsync<T>(T message, CancellationToken ct) where T : ICosmosCommand
    {
        return Task.CompletedTask;
    }

    //Send multiple commands
    public Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosCommand
    {
        //To be implemented
        return Task.CompletedTask;
    }

    //Publish a single event
    public async Task PublishAsync<T>(T message, CancellationToken ct) where T : ICosmosEvent
    {
        Console.WriteLine("CosmosBus publishAsync called");
        try
        {
            // Read the item to see if it exists.  
            Container container = _client.GetContainer("db", "items");
            ItemResponse<T> messageResponse = await container.ReadItemAsync<T>(message.id, new PartitionKey(message.topic), null, ct );
            Console.WriteLine("Item in database with id: {0} already exists\n", messageResponse.Resource.id);
        }
        catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Create an item in the container on topic
            Container container = _client.GetContainer("db", "items");
            ItemResponse<T> messageResponse = await container.CreateItemAsync<T>(message, new PartitionKey(message.topic), null, ct);

            // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
            Console.WriteLine("Created item in database with id: {0} Operation consumed {1} RUs.\n", messageResponse.Resource.id, messageResponse.RequestCharge);
        }
    }
    
    //Publish multiple events
    public Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosEvent
    {
        //To be implemented
        return Task.CompletedTask;
    }

    //Schedule a single command
    public Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand
    {
        //To be implemented
        return Task.CompletedTask;
    }

    //Schedule multiple commands
    public Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand
    {
        //To be implemented
        return Task.CompletedTask;
    }
}


