using System.Net;
using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace KnightBus.Cosmos;

public class CosmosCommandChannelReceiver<T> : IChannelReceiver
    where T : class, ICosmosCommand
{
    private readonly IProcessingSettings _settings;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageProcessor _processor;
    private readonly ICosmosConfiguration _cosmosConfiguration;
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosQueueClient<T> _cosmosQueueClient;

    public CosmosCommandChannelReceiver(
        IProcessingSettings processorSettings,
        IHostConfiguration config,
        IMessageProcessor processor,
        ICosmosConfiguration cosmosConfiguration,
        CosmosClient cosmosClient
    )
    {
        _settings = processorSettings;
        _hostConfiguration = config;
        _processor = processor;
        _cosmosConfiguration = cosmosConfiguration;
        _cosmosClient = cosmosClient;
        _cosmosQueueClient = new CosmosQueueClient<T>(cosmosConfiguration, null);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _cosmosQueueClient.StartAsync(_cosmosClient, cancellationToken);

        //Process messages in topic queue
        var commandProcessorBuilder = _cosmosQueueClient
            .TopicQueue.GetChangeFeedProcessorBuilder<InternalCosmosMessage<T>>(
                processorName: "Command-" + AutoMessageMapper.GetQueueName<T>(),
                onChangesDelegate: ProcessChangesAsync
            )
            .WithInstanceName($"consoleHost") //TODO must use unique name for parallel processing
            .WithLeaseContainer(_cosmosQueueClient.Lease)
            .WithStartTime(DateTime.Now - _cosmosConfiguration.StartRewind);

        if (_cosmosConfiguration.PollingDelay != null)
        {
            commandProcessorBuilder.WithPollInterval(_cosmosConfiguration.PollingDelay.Value);
        }
        var commandProcessor = commandProcessorBuilder.Build();

        await commandProcessor.StartAsync();
        _hostConfiguration.Log.LogInformation(
            "Receiver on {Topic} started",
            AutoMessageMapper.GetQueueName<T>()
        );
    }

    private async Task ProcessChangesAsync(
        ChangeFeedProcessorContext context,
        IReadOnlyCollection<InternalCosmosMessage<T>> messages,
        CancellationToken cancellationToken
    )
    {
        List<Task> tasks = new List<Task>();
        foreach (var message in messages)
        {
            var messageStateHandler = new CosmosMessageStateHandler<T>(
                _cosmosQueueClient,
                message,
                _settings.DeadLetterDeliveryLimit,
                _hostConfiguration.DependencyInjection
            );
            tasks.Add(_processor.ProcessAsync(messageStateHandler, cancellationToken));
        }
        await Task.WhenAll(tasks);
    }

    public IProcessingSettings Settings { get; set; }
}
