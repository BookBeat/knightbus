using System;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Cosmos.Messages;

namespace KnightBus.Cosmos;

public class CosmosSubscriptionChannelFactory : ITransportChannelFactory
{
    public CosmosSubscriptionChannelFactory(ICosmosConfiguration configuration)
    {
        Configuration = configuration;
    }

    public ITransportConfiguration Configuration { get; set; }

    public IChannelReceiver Create(Type messageType,
                                IEventSubscription subscription,
                                IProcessingSettings processingSettings,
                                IMessageSerializer serializer,
                                IHostConfiguration configuration,
                                IMessageProcessor processor)
    {
        Console.WriteLine("create IChannelReceiver called\n\n");
        // Dynamically create the Cosmos-specific channel receiver
        var queueReaderType = typeof(CosmosSubscriptionChannelReceiver<>).MakeGenericType(messageType);
        var queueReader = (IChannelReceiver)Activator.CreateInstance(queueReaderType, processingSettings, serializer, subscription, Configuration, configuration, processor);
        return queueReader;
    }

    public bool CanCreate(Type messageType)
    {
        // This checks whether the message type is compatible with Cosmos commands
        return typeof(ICosmosCommand).IsAssignableFrom(messageType) ||
               typeof(ICosmosEvent).IsAssignableFrom(messageType);
    }
}
