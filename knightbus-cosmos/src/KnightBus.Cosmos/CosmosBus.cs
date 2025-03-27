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
}

public class CosmosBus : ICosmosBus
{
    private readonly CosmosClient _client;
    private ICosmosConfiguration _cosmosConfiguration;
    //Constructor
    public CosmosBus(ICosmosConfiguration config, ICosmosConfiguration cosmosConfiguration)
    {
        _cosmosConfiguration = cosmosConfiguration;
        
        string? connectionString = config.ConnectionString;
        ArgumentNullException.ThrowIfNull(connectionString);
        //Instantiate CosmosClient
        _client = new CosmosClient(connectionString,
            new CosmosClientOptions() { ApplicationName = "publisher" });
    }

    public void cleanUp()
    {
        _client.Dispose();
    }
    
    //Send a single command
    public Task SendAsync<T>(T message, CancellationToken cancellationToken) where T : ICosmosCommand
    {
        return SendAsync([message], cancellationToken);
    }

    //Send multiple commands
    public async Task SendAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken) where T : ICosmosCommand
    {
        // Create an item in the container on topic
        Container container = _client.GetContainer(_cosmosConfiguration.Database, AutoMessageMapper.GetQueueName<T>());
        foreach (var message in messages)
        {
            var internalCosmosMessage = new InternalCosmosMessage<T>(message);
            ItemResponse<InternalCosmosMessage<T>> messageResponse =
                await container.CreateItemAsync<InternalCosmosMessage<T>>(internalCosmosMessage,
                    new PartitionKey(internalCosmosMessage.id), null, cancellationToken);
            Console.WriteLine(
                $"Created msg {internalCosmosMessage.id} on {AutoMessageMapper.GetQueueName<T>()} - {messageResponse.RequestCharge} RUs consumed");
        }
    }

    //Publish a single event
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : ICosmosEvent
    {

        return PublishAsync([message], cancellationToken);
    }
    
    //Publish multiple events
    public async Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken) where T : ICosmosEvent
    {
        // Create an item in the container on topic
        Container container = _client.GetContainer(_cosmosConfiguration.Database, AutoMessageMapper.GetQueueName<T>());
        foreach (var message in messages)
        {
            var internalCosmosMessage = new InternalCosmosMessage<T>(message);
            ItemResponse<InternalCosmosMessage<T>> messageResponse =
                await container.CreateItemAsync<InternalCosmosMessage<T>>(internalCosmosMessage,
                    new PartitionKey(internalCosmosMessage.id), null, cancellationToken);
            Console.WriteLine(
                $"Created event {internalCosmosMessage.id} on {AutoMessageMapper.GetQueueName<T>()} - {messageResponse.RequestCharge} RUs consumed");
        }
    }
}


