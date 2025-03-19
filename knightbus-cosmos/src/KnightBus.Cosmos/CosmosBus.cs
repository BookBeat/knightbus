using System.Net;
using KnightBus.Core;
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
    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : ICosmosEvent
    {
            // Create an item in the container on topic
            Container container = _client.GetContainer("db", "items");

            var internalCosmosMessage = new InternalCosmosMessage<T>(message);
            ItemResponse<InternalCosmosMessage<T>> messageResponse =
                await container.CreateItemAsync<InternalCosmosMessage<T>>(internalCosmosMessage, new PartitionKey(internalCosmosMessage.Topic), null, cancellationToken);
            Console.WriteLine($"Created item {internalCosmosMessage.id} on {internalCosmosMessage.Topic} - {messageResponse.RequestCharge} RUs consumed");
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


