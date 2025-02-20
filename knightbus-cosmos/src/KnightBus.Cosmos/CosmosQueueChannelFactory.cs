using System;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Cosmos.Messages;

namespace KnightBus.Cosmos;

public class CosmosQueueChannelFactory : ITransportChannelFactory
{
    public CosmosQueueChannelFactory(ICosmosConfiguration configuration)
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
        Console.WriteLine("create IChannelReceiver called");
        // Dynamically create the Cosmos-specific channel receiver
        var readerType = typeof(CosmosQueueChannelReceiver<>).MakeGenericType(messageType);
        var reader = (IChannelReceiver)Activator.CreateInstance(readerType, processingSettings, serializer, configuration, processor, Configuration, subscription);
        return reader;
    }

    public bool CanCreate(Type messageType)
    {
        // This checks whether the message type is compatible with Cosmos commands
        return typeof(ICosmosCommand).IsAssignableFrom(messageType) ||
               typeof(ICosmosEvent).IsAssignableFrom(messageType);
    }
}
