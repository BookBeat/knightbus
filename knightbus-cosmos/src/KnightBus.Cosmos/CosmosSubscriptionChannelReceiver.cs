using System.Collections.ObjectModel;
using System.Data;
using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public class CosmosSubscriptionChannelReceiver<T> : IChannelReceiver where T : class, ICosmosEvent
{
    private readonly IProcessingSettings settings;
    private readonly IMessageSerializer _serializer;
    private IEventSubscription _subscription;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageProcessor _processor;
    private readonly ICosmosConfiguration _cosmosConfiguration;
    private readonly CosmosQueueClient<T> _cosmosQueueClient;
    
    public CosmosSubscriptionChannelReceiver(
        IProcessingSettings processorSettings,
        IMessageSerializer serializer,
        IEventSubscription subscription, //Currently not used
        IHostConfiguration config,
        IMessageProcessor processor,
        ICosmosConfiguration cosmosConfiguration
    )
    {
        settings = processorSettings;
        _serializer = serializer;
        _subscription = subscription;
        _hostConfiguration = config;
        _processor = processor;
        _cosmosConfiguration = cosmosConfiguration;
        _cosmosQueueClient = new CosmosQueueClient<T>(_cosmosConfiguration);
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //CosmosQueueClient creates database and container to send and receive messages
        await _cosmosQueueClient.StartAsync(cancellationToken);
        
        //Start asynchronous change feed listening and processing changes on the topic
        StartPullModelChangeFeed(cancellationToken);
    }
    
    
    //Start pull model change feed
    private async Task StartPullModelChangeFeed(CancellationToken cancellationToken)
    {        
        var topic = AutoMessageMapper.GetQueueName<T>();

        //Start pull model listening only on the topic partition key
        var iteratorForPartitionKey = _cosmosQueueClient.Container.GetChangeFeedIterator<InternalCosmosMessage<T>>(
            ChangeFeedStartFrom.Beginning(FeedRange.FromPartitionKey(new PartitionKey(topic))), ChangeFeedMode.LatestVersion);
        
        Console.WriteLine($"started change feed listening for : {topic}");
        
        while (iteratorForPartitionKey.HasMoreResults)
        {
            //TODO: Consider using batch processing to optimize
            FeedResponse<InternalCosmosMessage<T>> response = await iteratorForPartitionKey.ReadNextAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                // 1 Request Unit used if no changes have been made
                await Task.Delay(_cosmosConfiguration.PollingDelay, cancellationToken);
            }
            else
            {
                foreach (InternalCosmosMessage<T> message in response)
                {
                    //Processing of messages can be done asynchronously
                    ProcessMessageAsync(message, cancellationToken);
                }
            }
        }
    }

    private async Task ProcessMessageAsync(InternalCosmosMessage<T> message, CancellationToken cancellationToken)
    {
        var messageStateHandler = new CosmosMessageStateHandler<T>(_cosmosQueueClient, message, settings.DeadLetterDeliveryLimit, _serializer, _hostConfiguration.DependencyInjection);
        await _processor.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
    }
    
    public IProcessingSettings Settings { get; set; }
}
