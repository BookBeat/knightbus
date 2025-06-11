using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public class CosmosQueueClient<T>
    where T : class, IMessage
{
    private readonly ICosmosConfiguration _cosmosConfiguration;
    private Database Database { get; set; }
    public Container TopicQueue { get; private set; }
    public Container? RetryQueue { get; private set; }
    public Container Lease { get; private set; }
    private Container DeadLetterQueue { get; set; }
    private IEventSubscription? Subscription { get; }

    public CosmosQueueClient(
        ICosmosConfiguration cosmosConfiguration,
        IEventSubscription? subscription
    )
    {
        _cosmosConfiguration = cosmosConfiguration;
        Subscription = subscription;
    }

    public async Task StartAsync(CosmosClient cosmosClient, CancellationToken cancellationToken)
    {
        //Get database, create if it does not exist
        DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(
            id: _cosmosConfiguration.Database,
            throughputProperties: ThroughputProperties.CreateAutoscaleThroughput(
                _cosmosConfiguration.MaxRUs
            ),
            cancellationToken: cancellationToken
        );
        CheckHttpResponse(databaseResponse.StatusCode, _cosmosConfiguration.Database);
        Database = databaseResponse.Database;

        Lease = await GetOrCreateContainerAsync(
            _cosmosConfiguration.LeaseContainer,
            cancellationToken
        );

        TopicQueue = await GetOrCreateContainerAsync(
            AutoMessageMapper.GetQueueName<T>(),
            cancellationToken
        );

        DeadLetterQueue = await GetOrCreateContainerAsync(
            AutoMessageMapper.GetQueueName<T>() + "_DL",
            cancellationToken,
            "/Subscription",
            -1 // TTL = -1 means items never expire
        );

        //Only events should have retryQueue
        if (typeof(ICosmosEvent).IsAssignableFrom(typeof(T))) //TODO check if Subscription == null?
            RetryQueue = await GetOrCreateContainerAsync(
                AutoMessageMapper.GetQueueName<T>() + "_Retry_" + Subscription!.Name,
                cancellationToken
            );
    }

    private async Task<Container> GetOrCreateContainerAsync(
        string id,
        CancellationToken cancellationToken,
        string partitionKeyPath = "/id",
        int? TTL = null
    )
    {
        TTL ??= (int)_cosmosConfiguration.DefaultTimeToLive.TotalSeconds; //Get default TTL if not specified

        //Create Container if it does not exist
        //Replace it if it exists and partitionKey or default TTL does not match the specified values
        //Existing documents in the container are not removed
        try
        {
            Container container = Database.GetContainer(id);
            ContainerProperties properties = (
                await container.ReadContainerAsync(cancellationToken: cancellationToken)
            ).Resource;

            if (
                properties.PartitionKeyPath == partitionKeyPath
                && properties.DefaultTimeToLive == TTL
            )
            {
                return container;
            }

            properties.PartitionKeyPath = partitionKeyPath;
            properties.DefaultTimeToLive = TTL;

            await container.ReplaceContainerAsync(properties, cancellationToken: cancellationToken);

            return container;
        }
        catch
        {
            //Create container if it does not exist
            var response = await Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(id, partitionKeyPath) { DefaultTimeToLive = TTL },
                cancellationToken: cancellationToken
            );
            CheckHttpResponse(response.StatusCode, id);
            return response.Container;
        }
    }

    private static void CheckHttpResponse(HttpStatusCode statusCode, string id)
    {
        if (statusCode != HttpStatusCode.OK && statusCode != HttpStatusCode.Created)
            throw new Exception($"Unexpected http response when creating {id} : {statusCode}");
    }

    public async Task CompleteAsync(InternalCosmosMessage<T> message)
    {
        await Task.CompletedTask;
    }

    public async Task AbandonByErrorAsync(InternalCosmosMessage<T> message)
    {
        message.DeliveryCount++;
        //Upsert item into retryQueue
        ItemResponse<InternalCosmosMessage<T>> response = await RetryQueue.UpsertItemAsync<
            InternalCosmosMessage<T>
        >(message, new PartitionKey(message.id));
    }

    public async Task DeadLetterAsync(InternalCosmosMessage<T> message)
    {
        DeadLetterCosmosMessage<T> deadLetterMessage = message.ToDeadLetterMessage(
            AutoMessageMapper.GetQueueName<T>(),
            Subscription!.Name
        );
        await DeadLetterQueue.CreateItemAsync(
            deadLetterMessage,
            new PartitionKey(deadLetterMessage.Subscription)
        );
    }
}
