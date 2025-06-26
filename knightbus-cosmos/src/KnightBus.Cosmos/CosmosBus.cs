using System.Collections.Concurrent;
using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public interface ICosmosBus
{
    Task SendAsync<T>(T message, CancellationToken ct)
        where T : ICosmosCommand;

    Task SendAsync<T>(IEnumerable<T> messages, CancellationToken ct)
        where T : ICosmosCommand;

    Task PublishAsync<T>(T message, CancellationToken ct)
        where T : ICosmosEvent;

    Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken ct)
        where T : ICosmosEvent;
}

public class CosmosBus(CosmosClient client, ICosmosConfiguration cosmosConfiguration) : ICosmosBus
{
    //Send a single command
    public Task SendAsync<T>(T message, CancellationToken cancellationToken)
        where T : ICosmosCommand
    {
        return InternalInsert(message, cancellationToken);
    }

    //Send multiple commands
    public async Task SendAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken)
        where T : ICosmosCommand
    {
        await InternalInsert(messages, cancellationToken);
    }

    //Publish a single event
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken)
        where T : ICosmosEvent
    {
        return InternalInsert(message, cancellationToken);
    }

    //Publish multiple events
    public async Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken)
        where T : ICosmosEvent
    {
        await InternalInsert(messages, cancellationToken);
    }

    //Insert a single message
    private async Task InternalInsert<T>(T message, CancellationToken cancellationToken)
        where T : IMessage
    {
        Container container = client.GetContainer(
            cosmosConfiguration.Database,
            AutoMessageMapper.GetQueueName<T>()
        );

        var internalMessage = new InternalCosmosMessage<T>(
            message,
            cosmosConfiguration.DefaultTimeToLive
        );

        try
        {
            await container.CreateItemAsync(
                internalMessage,
                new PartitionKey(internalMessage.id),
                cancellationToken: cancellationToken
            );
        }
        catch (CosmosException cosmosException)
        {
            Console.WriteLine(
                $"CosmosException: {cosmosException.StatusCode} ({cosmosException.ResponseBody})." //TODO throw?
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Exception: {ex}." //TODO throw?
            );
        }
    }

    //Internal function to insert multiple messages into cosmos
    private async Task InternalInsert<T>(
        IEnumerable<T> messages,
        CancellationToken cancellationToken
    )
        where T : IMessage
    {
        Container container = client.GetContainer(
            cosmosConfiguration.Database,
            AutoMessageMapper.GetQueueName<T>()
        );

        //Insert messages asynchronously to utilize bulk execution
        List<Task> tasks = new List<Task>();
        var failedMessages = new ConcurrentBag<T>();

        foreach (var msg in messages)
        {
            var internalMessage = new InternalCosmosMessage<T>(
                msg,
                cosmosConfiguration.DefaultTimeToLive
            );
            tasks.Add(
                container
                    .CreateItemAsync(
                        internalMessage,
                        new PartitionKey(internalMessage.id),
                        cancellationToken: cancellationToken
                    )
                    .ContinueWith(
                        itemResponse =>
                        {
                            //Error handling for failed messages
                            if (!itemResponse.IsCompletedSuccessfully)
                            {
                                AggregateException? innerExceptions =
                                    itemResponse.Exception?.Flatten();
                                CosmosException? cosmosException = innerExceptions
                                    ?.InnerExceptions.OfType<CosmosException>()
                                    .FirstOrDefault();
                                //Add messages that fail due to rate limitations to list that will be retried later
                                if (cosmosException != null)
                                {
                                    Console.WriteLine(
                                        $"CosmosException: {cosmosException.StatusCode} ({cosmosException.ResponseBody})." //TODO throw?
                                    );
                                    //TODO Log exception
                                }
                                //Log other exceptions
                                else
                                {
                                    Console.WriteLine(
                                        $"Exception: {innerExceptions?.InnerExceptions.FirstOrDefault()}." //TODO throw?
                                    );
                                    //TODO Log exception
                                }
                            }
                        },
                        cancellationToken
                    )
            );
        }

        await Task.WhenAll(tasks);
    }
}
