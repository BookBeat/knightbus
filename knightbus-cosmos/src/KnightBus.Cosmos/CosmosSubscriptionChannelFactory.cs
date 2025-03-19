using System;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Cosmos.Messages;

namespace KnightBus.Cosmos;

public class CosmosSubscriptionChannelFactory : ITransportChannelFactory
{
    
    public ICosmosConfiguration CosmosConfiguration { get; }
    public ITransportConfiguration Configuration { get; set; }
    public CosmosSubscriptionChannelFactory(ICosmosConfiguration configuration)
    {
        CosmosConfiguration = configuration;
        Configuration = configuration;
    }
    

    public IChannelReceiver? Create(Type messageType,
        IEventSubscription subscription,
        IProcessingSettings processingSettings,
        IMessageSerializer serializer,
        IHostConfiguration hostConfiguration,
        IMessageProcessor processor)
    {
        // Dynamically create the Cosmos-specific channel receiver
        var queueReaderType = typeof(CosmosSubscriptionChannelReceiver<>).MakeGenericType(messageType);
        var queueReader = Activator.CreateInstance(
            queueReaderType,
            processingSettings,
            serializer,
            subscription,
            hostConfiguration,
            processor,
            CosmosConfiguration) as IChannelReceiver;
        return queueReader;
    }

    public bool CanCreate(Type messageType)
    {
        return typeof(ICosmosEvent).IsAssignableFrom(messageType);
    }
}
