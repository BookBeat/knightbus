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
    private readonly ICosmosConfiguration _cosmosConfiguration;
    //Constructor
    public CosmosBus(ICosmosConfiguration config, ICosmosConfiguration cosmosConfiguration)
    {
        _cosmosConfiguration = cosmosConfiguration;
        
        string? connectionString = config.ConnectionString;
        ArgumentNullException.ThrowIfNull(connectionString);
        //Instantiate CosmosClient
        Console.WriteLine("Publisher started cosmos client");
        _client = new CosmosClient(connectionString,
            new CosmosClientOptions() { ApplicationName = "publisher"}); //, AllowBulkExecution = true});
    }

    public void CleanUp()
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
        await InternalInsert(messages, cancellationToken);
    }

    //Publish a single event
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken) where T : ICosmosEvent
    {

        return PublishAsync([message], cancellationToken);
    }
    
    //Publish multiple events
    public async Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken) where T : ICosmosEvent
    {
        await InternalInsert(messages, cancellationToken);
    }
    
    private async Task InternalInsert<T> (IEnumerable<T> messages, CancellationToken cancellationToken) where T : IMessage
    {
        // Create an item in the container on topic
        Container container = _client.GetContainer(_cosmosConfiguration.Database, AutoMessageMapper.GetQueueName<T>());

        // Optimized for bulk execution
        List<Task> tasks = new List<Task>();
        foreach (var msg in messages)
        {
            var internalMessage = new InternalCosmosMessage<T>(msg);
            tasks.Add(container.CreateItemAsync(internalMessage, new PartitionKey(internalMessage.id)).ContinueWith(
                itemResponse =>
                {
                    if (!itemResponse.IsCompletedSuccessfully)
                    {
                        AggregateException innerExceptions = itemResponse.Exception.Flatten();
                        if (innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException) is CosmosException cosmosException)
                        {
                            Console.WriteLine($"Received {cosmosException.StatusCode} ({cosmosException.Message}).");
                        }
                        else
                        {
                            Console.WriteLine($"Exception {innerExceptions.InnerExceptions.FirstOrDefault()}.");
                        }
                    }
                }));
        }

        await Task.WhenAll(tasks);
    }
}


