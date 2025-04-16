using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace KnightBus.Cosmos;

public class CosmosSubscriptionChannelReceiver<T> : IChannelReceiver where T : class, ICosmosEvent
{
    private readonly IMessageSerializer _serializer;
    private IEventSubscription _subscription;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageProcessor _processor;
    private readonly ICosmosConfiguration _cosmosConfiguration;
    private CosmosClient _cosmosClient;
    private readonly CosmosQueueClient<T> _cosmosQueueClient;
    
    public CosmosSubscriptionChannelReceiver(
        IProcessingSettings processorSettings,
        IMessageSerializer serializer,
        IEventSubscription subscription,
        IHostConfiguration config,
        IMessageProcessor processor,
        ICosmosConfiguration cosmosConfiguration,
        CosmosClient cosmosClient
    )
    {
        Settings = processorSettings;
        _serializer = serializer;
        _subscription = subscription;
        _hostConfiguration = config;
        _processor = processor;
        _cosmosConfiguration = cosmosConfiguration;
        _cosmosClient = cosmosClient;
        _cosmosQueueClient = new CosmosQueueClient<T>(cosmosConfiguration,subscription);
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _cosmosQueueClient.StartAsync(_cosmosClient, cancellationToken);
        
        //Process event on topic
        ChangeFeedProcessor eventFetcher = _cosmosQueueClient.TopicQueue
            .GetChangeFeedProcessorBuilder<InternalCosmosMessage<T>>(processorName: AutoMessageMapper.GetQueueName<T>() + "->" + _subscription.Name, onChangesDelegate: ProcessChangesAsync)
            .WithInstanceName($"consoleHost") //Must use program variable for parallel processing
            .WithLeaseContainer(_cosmosQueueClient.Lease)
            .WithPollInterval(_cosmosConfiguration.PollingDelay)
            .Build();
        
        await eventFetcher.StartAsync();
        Console.WriteLine($"{_subscription.Name} processor on topic {AutoMessageMapper.GetQueueName<T>()} started.");
        
        
        //Process messages in Retry queue
        ChangeFeedProcessor eventProcessor = _cosmosQueueClient.RetryQueue
            .GetChangeFeedProcessorBuilder<InternalCosmosMessage<T>>(processorName: _subscription.Name, onChangesDelegate: ProcessChangesAsync)
            .WithInstanceName($"consoleHost") //Must use program variable for parallel processing
            .WithLeaseContainer(_cosmosQueueClient.Lease)
            .WithPollInterval(_cosmosConfiguration.PollingDelay)
            .Build();
        
        await eventProcessor.StartAsync();
        Console.WriteLine($"{_subscription.Name} retry processor for started.");
    }
    private async Task ProcessChangesAsync(ChangeFeedProcessorContext context, IReadOnlyCollection<InternalCosmosMessage<T>> messages, CancellationToken cancellationToken)
    {
        List<Task> tasks = new List<Task>();
        foreach (var message in messages)
        {
            var messageStateHandler = new CosmosMessageStateHandler<T>(_cosmosQueueClient, message, Settings.DeadLetterDeliveryLimit, _hostConfiguration.DependencyInjection);
            tasks.Add(_processor.ProcessAsync(messageStateHandler, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }
    
    public IProcessingSettings Settings { get; set; }
}
