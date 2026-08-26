using System;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus;

internal class ServiceBusQueueChannelFactory : ITransportChannelFactory
{
    public ServiceBusQueueChannelFactory(IServiceBusConfiguration configuration)
    {
        Configuration = configuration;
    }

    public ITransportConfiguration Configuration { get; set; }

    public IChannelReceiver Create(
        Type messageType,
        IEventSubscription? subscription,
        IProcessingSettings processingSettings,
        IMessageSerializer serializer,
        IHostConfiguration configuration,
        IMessageProcessor processor
    )
    {
        var queueReaderType = typeof(ServiceBusQueueChannelReceiver<>).MakeGenericType(messageType);
        var queueReader =
            Activator.CreateInstance(
                queueReaderType,
                processingSettings,
                serializer,
                Configuration,
                configuration,
                processor
            ) as IChannelReceiver;
        return queueReader
            ?? throw new InvalidOperationException("ChannelReceiver could not be created");
    }

    public bool CanCreate(Type messageType)
    {
        return typeof(IServiceBusCommand).IsAssignableFrom(messageType);
    }
}
