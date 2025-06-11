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
    public void Dispose()
    {
        client.Dispose();
    }

    //Send a single command
    public Task SendAsync<T>(T message, CancellationToken cancellationToken)
        where T : ICosmosCommand
    {
        return InternalInsert([message], cancellationToken);
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
        return InternalInsert([message], cancellationToken);
    }

    //Publish multiple events
    public async Task PublishAsync<T>(IEnumerable<T> messages, CancellationToken cancellationToken)
        where T : ICosmosEvent
    {
        await InternalInsert(messages, cancellationToken);
    }

    //Internal function to insert messages into cosmos
    private async Task InternalInsert<T>(
        IEnumerable<T> messages,
        CancellationToken cancellationToken
    )
        where T : IMessage
    {
        // Get container
        Container container = client.GetContainer(
            cosmosConfiguration.Database,
            AutoMessageMapper.GetQueueName<T>()
        );

        int retries = 0;
        while (retries <= cosmosConfiguration.MaxPublishRetriesOnRateLimited)
        {
            //Insert messages asynchronously to utilize bulk execution
            List<Task> tasks = new List<Task>();
            var failedMessages = new ConcurrentBag<T>();

            foreach (var msg in messages)
            {
                var internalMessage = new InternalCosmosMessage<T>(msg);
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
                                    if (
                                        cosmosException is
                                        { StatusCode: HttpStatusCode.TooManyRequests }
                                    )
                                    {
                                        failedMessages.Add(msg);
                                    }
                                    //Log cosmosException
                                    else if (cosmosException != null)
                                    {
                                        Console.WriteLine(
                                            $"CosmosException: {cosmosException.StatusCode} ({cosmosException.ResponseBody})." //TODO throw?
                                        );
                                    }
                                    //Log other exceptions
                                    else
                                    {
                                        Console.WriteLine(
                                            $"Exception: {innerExceptions?.InnerExceptions.FirstOrDefault()}." //TODO throw?
                                        );
                                    }
                                }
                            },
                            cancellationToken
                        )
                );
            }

            await Task.WhenAll(tasks);

            if (failedMessages.IsEmpty)
                return;

            retries += 1;
            messages = failedMessages;

            double failRate = (double)failedMessages.Count / messages.Count();
            //Wait before retrying to publish the events
            await Task.Delay(TimeSpan.FromSeconds(10 * failRate), cancellationToken); //10 is arbitrary
        }

        Console.WriteLine($"{messages.Count()} messages could not be sent"); //TODO throw?
    }
}
