using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;
public interface ICosmosBus
{
    
    Task SendAsync<T>(T message, CancellationToken ct) where T : ICosmosCommand;
    
    Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosCommand;
    
    Task PublishAsync<T>(T message, CancellationToken ct) where T : ICosmosEvent;
    
    Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosEvent;
    
    Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand;
    
    Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand;
}

public class CosmosBus : ICosmosBus
{
    private readonly CosmosClient _client;
    private readonly Container _container;
    private ICosmosConfiguration _cosmosConfiguration;
    //Constructor
    public CosmosBus(ICosmosConfiguration config, ICosmosConfiguration cosmosConfiguration)
    {
        _cosmosConfiguration = cosmosConfiguration;
        
        string? connectionString = config.ConnectionString;
        ArgumentNullException.ThrowIfNull(connectionString);
        //Instantiate CosmosClient
        _client = new CosmosClient(connectionString,
            new CosmosClientOptions() { ApplicationName = "Sender" });
        
        // Create an item in the container on topic
        _container = _client.GetContainer(_cosmosConfiguration.Database, _cosmosConfiguration.Container);
    }

    public void cleanUp()
    {
        _client.Dispose();
    }
    
    //Send a single command
    public Task SendAsync<T>(T message, CancellationToken ct) where T : ICosmosCommand
    {
        //Not implemented, should be made async
        return Task.CompletedTask;
    }

    //Send multiple commands
    public Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosCommand
    {
        //Not implemented, should be made async
        return Task.CompletedTask;
    }

    //Publish a single event
    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : ICosmosEvent
    {

            var internalCosmosMessage = new InternalCosmosMessage<T>(message);
            ItemResponse<InternalCosmosMessage<T>> messageResponse =
                await _container.CreateItemAsync<InternalCosmosMessage<T>>(internalCosmosMessage, new PartitionKey(internalCosmosMessage.Topic), null, cancellationToken);
            Console.WriteLine($"Created item {internalCosmosMessage.id} on {internalCosmosMessage.Topic} - {messageResponse.RequestCharge} RUs consumed");
    }
    
    //Publish multiple events
    public async Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct) where T : ICosmosEvent
    {

        foreach (var message in messages)
        {
            await PublishAsync(message, ct);
        }
    }

    //Schedule a single command
    public Task ScheduleAsync<T>(T message, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand
    {
        //To be implemented
        return Task.FromException(new NotImplementedException());
    }

    //Schedule multiple commands
    public Task ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan delay, CancellationToken ct) where T : ICosmosCommand
    {
        //To be implemented
        return Task.FromException(new NotImplementedException());
    }
}


