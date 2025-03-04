using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;

namespace KnightBus.Cosmos;

public class CosmosSubscriptionChannelReceiver<T> : IChannelReceiver where T : class, ICosmosEvent
{
    private CosmosClient _cosmosClient;
    private IProcessingSettings _processorSettings;
    private IMessageSerializer _serializer;
    private IEventSubscription _subscription;
    private IHostConfiguration _configuration;
    private IMessageProcessor _processor;
    
    public CosmosSubscriptionChannelReceiver(
        IProcessingSettings processorSettings,
        IMessageSerializer serializer,
        IEventSubscription subscription,
        IHostConfiguration config,
        IMessageProcessor processor
    )
    {
        _processorSettings = processorSettings;
        _serializer = serializer;
        _subscription = subscription;
        _configuration = config;
        _processor = processor;

    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _queueClient = 
        
        Console.WriteLine("Not implemented yet");
        throw new NotImplementedException();
    }
    
    public IProcessingSettings Settings { get; set; }
}
