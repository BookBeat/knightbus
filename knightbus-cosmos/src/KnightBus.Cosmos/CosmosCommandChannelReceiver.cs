using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace KnightBus.Cosmos;

public class CosmosCommandChannelReceiver<T> : IChannelReceiver where T : class, ICosmosCommand
{
    private readonly IProcessingSettings settings;
    private readonly IMessageSerializer _serializer;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageProcessor _processor;
    private readonly ICosmosConfiguration _cosmosConfiguration;
    private readonly CosmosQueueClient<T> _cosmosQueueClient;
    
    
    public CosmosCommandChannelReceiver(
        IProcessingSettings processorSettings,
        IMessageSerializer serializer,
        IHostConfiguration config,
        IMessageProcessor processor,
        ICosmosConfiguration cosmosConfiguration
    )
    {
        settings = processorSettings;
        _serializer = serializer;
        _hostConfiguration = config;
        _processor = processor;
        _cosmosConfiguration = cosmosConfiguration;
        _cosmosQueueClient = new CosmosQueueClient<T>(cosmosConfiguration,null);
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _cosmosQueueClient.StartAsync(cancellationToken);
        
        //Process messages in subscription queue
        ChangeFeedProcessor changeFeedProcessor = _cosmosQueueClient.PersonalQueue
            .GetChangeFeedProcessorBuilder<InternalCosmosMessage<T>>(processorName: "Command-" + AutoMessageMapper.GetQueueName<T>(), onChangesDelegate: ProcessChangesAsync)
            .WithInstanceName($"consoleHost") //Must use program variable or machine name for parallel processing
            .WithLeaseContainer(_cosmosQueueClient.Lease)
            .WithPollInterval(_cosmosConfiguration.PollingDelay)
            .Build();
        
        await changeFeedProcessor.StartAsync();
        Console.WriteLine($"Processor on Command-{AutoMessageMapper.GetQueueName<T>()} started.");
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
