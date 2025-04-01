using System;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Cosmos.Messages;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public class CosmosCommandChannelFactory : ITransportChannelFactory
{
    
    public ICosmosConfiguration CosmosConfiguration { get; }
    public ITransportConfiguration Configuration { get; set; }
    
    private CosmosClient CosmosClient { get; set; }
    public CosmosCommandChannelFactory(ICosmosConfiguration configuration)
    {
        CosmosConfiguration = configuration;
        Configuration = configuration;
        Console.WriteLine("Command channel created cosmosClient");
        CosmosClient = new CosmosClient(CosmosConfiguration.ConnectionString);
    }
    

    public IChannelReceiver Create(Type messageType,
        IEventSubscription subscription,
        IProcessingSettings processingSettings,
        IMessageSerializer serializer,
        IHostConfiguration hostConfiguration,
        IMessageProcessor processor)
    {
        // Dynamically create the Cosmos-specific channel receiver
        var queueReaderType = typeof(CosmosCommandChannelReceiver<>).MakeGenericType(messageType);
        var queueReader = Activator.CreateInstance(
            queueReaderType,
            processingSettings,
            serializer,
            hostConfiguration,
            processor,
            CosmosConfiguration,
            CosmosClient) as IChannelReceiver;
        return queueReader ?? throw new InvalidOperationException("ChannelReceiver could not be created");
    }

    public bool CanCreate(Type messageType)
    {
        return typeof(ICosmosCommand).IsAssignableFrom(messageType);
    }
}
