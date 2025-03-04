using System;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Cosmos.Messages;

namespace KnightBus.Cosmos;

public class CosmosSubscriptionChannelFactory : ITransportChannelFactory
{
    public CosmosSubscriptionChannelFactory(ICosmosConfiguration configuration)
    {
        Console.WriteLine("CosmosSubscriptionChannelFactory initialized");
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
        Console.WriteLine("Create cosmosSubscriptionChannelReceiver called");
        // Dynamically create the Cosmos-specific channel receiver
        var queueReaderType = typeof(CosmosSubscriptionChannelReceiver<>).MakeGenericType(messageType);
        var queueReader = Activator.CreateInstance(
            queueReaderType,
            processingSettings,
            serializer,
            subscription,
            configuration,
            processor) as IChannelReceiver;
        Console.WriteLine(queueReader);
        Console.WriteLine($"null? : {queueReader == null}");
        return queueReader;
    }

    public bool CanCreate(Type messageType)
    {
        return typeof(ICosmosEvent).IsAssignableFrom(messageType);
    }
}
