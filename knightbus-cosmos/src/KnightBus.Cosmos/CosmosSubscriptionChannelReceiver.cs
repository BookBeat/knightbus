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
        await _cosmosQueueClient.StartAsync(cancellationToken);
        
        //Start asynchronous change feed listening and processing changes on the topic
        
            ChangeFeedProcessor changeFeedProcessor = _cosmosQueueClient.Container
                .GetChangeFeedProcessorBuilder<InternalCosmosMessage<T>>(processorName: "changeFeed", onChangesDelegate: ProcessChangesAsync)
                .WithInstanceName("consoleHost")
                .WithLeaseContainer(_cosmosQueueClient.Lease)
                .WithPollInterval(_cosmosConfiguration.PollingDelay)
                .Build();
            
            await changeFeedProcessor.StartAsync();
            Console.WriteLine($"Change Feed Processor on {AutoMessageMapper.GetQueueName<T>()} started.");
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
