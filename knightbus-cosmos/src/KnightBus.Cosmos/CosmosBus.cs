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
    public CosmosBus(CosmosClient client, ICosmosConfiguration cosmosConfiguration)
    {
        _cosmosConfiguration = cosmosConfiguration;
        
        string? connectionString = cosmosConfiguration.ConnectionString;
        ArgumentNullException.ThrowIfNull(connectionString);
        _client = client;
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

    //Internal function to insert messages into cosmos
    private async Task InternalInsert<T>(IEnumerable<T> messages, CancellationToken cancellationToken) where T : IMessage
    {
        // Get container
        Container container = _client.GetContainer(_cosmosConfiguration.Database, AutoMessageMapper.GetQueueName<T>());
        
        const int maxClientRetries = 5; //Could be made into configurable setting
        
        int retryCount = 0;
        
        while (retryCount <= maxClientRetries)
        {
            
            //Insert messages asynchronously to utilize bulk execution
            List<Task> tasks = new List<Task>();
            var failedMessages = new List<T>();
            
            foreach (var msg in messages)
            {
                var internalMessage = new InternalCosmosMessage<T>(msg);
                tasks.Add(container.CreateItemAsync(internalMessage, new PartitionKey(internalMessage.id), cancellationToken: cancellationToken)
                    .ContinueWith(itemResponse =>
                    {
                        //Error handling for failed messages
                        if (!itemResponse.IsCompletedSuccessfully)
                        {
                            AggregateException? innerExceptions = itemResponse.Exception?.Flatten();
                            CosmosException? cosmosException = innerExceptions?.InnerExceptions
                                .OfType<CosmosException>()
                                .FirstOrDefault();
                            //Add Rate limited messages to list that will be retried later
                            if (cosmosException is { StatusCode: HttpStatusCode.TooManyRequests })
                            {
                                failedMessages.Add(msg);
                            }
                            //Log cosmosException
                            else if (cosmosException != null)
                            {
                                Console.WriteLine(
                                    $"CosmosException: {cosmosException.StatusCode} ({cosmosException.ResponseBody}).");
                            }
                            //Log other exceptions 
                            else
                            {
                                Console.WriteLine($"Exception: {innerExceptions?.InnerExceptions.FirstOrDefault()}.");
                            }
                        }
                    }, cancellationToken));
            }

            await Task.WhenAll(tasks);
            if (failedMessages.Count == 0)
            {
                Console.WriteLine("All messages inserted");
                return;
            }
            
            retryCount++;
            messages = failedMessages;
            if(retryCount <= maxClientRetries)
                Console.WriteLine($"Retrying {failedMessages.Count} messages ...");
            
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
        
        Console.WriteLine($"{messages.Count()} messages could not be sent");
    }
}


