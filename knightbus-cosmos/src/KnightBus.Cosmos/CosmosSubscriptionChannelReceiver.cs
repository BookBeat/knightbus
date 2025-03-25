using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger _logger;
    
    
    public CosmosSubscriptionChannelReceiver(
        IProcessingSettings processorSettings,
        IMessageSerializer serializer,
        IEventSubscription subscription,
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
        _cosmosQueueClient = new CosmosQueueClient<T>(cosmosConfiguration,subscription);
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _cosmosQueueClient.StartAsync(cancellationToken);
        
        
        //Fetch messages from topic to subscription queue
        ChangeFeedProcessor fetchFeed = _cosmosQueueClient.TopicContainer
            .GetChangeFeedProcessorBuilder<InternalCosmosMessage<T>>(processorName: AutoMessageMapper.GetQueueName<T>() + "->" + _subscription.Name, onChangesDelegate: FetchChangesAsync)
            .WithInstanceName($"consoleHost") //Must use program variable for parallel processing
            .WithLeaseContainer(_cosmosQueueClient.Lease)
            .WithPollInterval(_cosmosConfiguration.PollingDelay)
            .Build();
        
        await fetchFeed.StartAsync();
        Console.WriteLine($"Fetcher on topic {AutoMessageMapper.GetQueueName<T>()} started.");
        
        
        //Process messages in subscription queue
        ChangeFeedProcessor changeFeedProcessor = _cosmosQueueClient.SubscriptionContainer
            .GetChangeFeedProcessorBuilder<InternalCosmosMessage<T>>(processorName: _subscription.Name, onChangesDelegate: ProcessChangesAsync)
            .WithInstanceName($"consoleHost") //Must use program variable for parallel processing
            .WithLeaseContainer(_cosmosQueueClient.Lease)
            .WithPollInterval(_cosmosConfiguration.PollingDelay)
            .Build();
        
        await changeFeedProcessor.StartAsync();
        Console.WriteLine($"Processor on {_subscription.Name} started.");
    }

    private async Task FetchChangesAsync(ChangeFeedProcessorContext context, IReadOnlyCollection<InternalCosmosMessage<T>> messages, CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            await _cosmosQueueClient.AddItemAsync(message, _cosmosQueueClient.SubscriptionContainer);
        }
    }
    
    private async Task ProcessChangesAsync(ChangeFeedProcessorContext context, IReadOnlyCollection<InternalCosmosMessage<T>> messages, CancellationToken cancellationToken)

    {
        foreach (var message in messages)
        {
            var messageStateHandler = new CosmosMessageStateHandler<T>(_cosmosQueueClient, message, settings.DeadLetterDeliveryLimit, _hostConfiguration.DependencyInjection);
            await _processor.ProcessAsync(messageStateHandler, CancellationToken.None).ConfigureAwait(false);
        }
    }
    
    public IProcessingSettings Settings { get; set; }
}
