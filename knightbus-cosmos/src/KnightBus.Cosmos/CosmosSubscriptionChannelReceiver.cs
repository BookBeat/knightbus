using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace KnightBus.Cosmos;

public class CosmosSubscriptionChannelReceiver<T> : IChannelReceiver
    where T : class, ICosmosEvent
{
    private readonly IMessageSerializer _serializer;
    private IEventSubscription _subscription;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageProcessor _processor;
    private readonly ICosmosConfiguration _cosmosConfiguration;
    private CosmosClient _cosmosClient;
    private readonly CosmosQueueClient<T> _cosmosQueueClient;
    public IProcessingSettings Settings { get; set; }

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
        _cosmosQueueClient = new CosmosQueueClient<T>(cosmosConfiguration, subscription);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _cosmosQueueClient.StartAsync(_cosmosClient, cancellationToken);

        //Process events directly on topic
        var eventProcessorBuilder = _cosmosQueueClient
            .TopicQueue.GetChangeFeedProcessorBuilder<InternalCosmosMessage<T>>(
                processorName: $"{_subscription.Name}-Retry",
                onChangesDelegate: ProcessChangesAsync
            )
            .WithInstanceName($"consoleHost") //Must use program variable for parallel processing
            .WithLeaseContainer(_cosmosQueueClient.Lease)
            .WithStartTime(DateTime.Now - _cosmosConfiguration.StartRewind);

        //Process events in Retry queue
        var retryProcessorBuilder = _cosmosQueueClient
            .RetryQueue!.GetChangeFeedProcessorBuilder<InternalCosmosMessage<T>>(
                processorName: _subscription.Name,
                onChangesDelegate: ProcessChangesAsync
            )
            .WithInstanceName($"consoleHost") //Must use program variable for parallel processing
            .WithLeaseContainer(_cosmosQueueClient.Lease);

        if (_cosmosConfiguration.PollingDelay != null)
        {
            eventProcessorBuilder.WithPollInterval(_cosmosConfiguration.PollingDelay.Value);
            retryProcessorBuilder.WithPollInterval(_cosmosConfiguration.PollingDelay.Value);
        }
        var retryProcessor = retryProcessorBuilder.Build();
        await retryProcessor.StartAsync();

        var eventProcessor = eventProcessorBuilder.Build();
        await eventProcessor.StartAsync();

        _hostConfiguration.Log.LogInformation("Sub {subscriptionName} started", _subscription.Name);
    }

    private async Task ProcessChangesAsync(
        ChangeFeedProcessorContext context,
        IReadOnlyCollection<InternalCosmosMessage<T>> messages,
        CancellationToken cancellationToken
    )
    {
        List<Task> tasks = [];
        foreach (var message in messages)
        {
            var messageStateHandler = new CosmosMessageStateHandler<T>(
                _cosmosQueueClient,
                message,
                Settings.DeadLetterDeliveryLimit,
                _hostConfiguration.DependencyInjection
            );
            tasks.Add(_processor.ProcessAsync(messageStateHandler, cancellationToken));
        }

        await Task.WhenAll(tasks);
    }
}
