using System;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus;

internal class ServiceBusTopicChannelFactory : ITransportChannelFactory
{
    public ServiceBusTopicChannelFactory(IServiceBusConfiguration configuration)
    {
        Configuration = configuration;
    }

    public ITransportConfiguration Configuration { get; set; }

    public IChannelReceiver Create(
        Type messageType,
        IEventSubscription subscription,
        IProcessingSettings processingSettings,
        IMessageSerializer serializer,
        IHostConfiguration configuration,
        IMessageProcessor processor
    )
    {
        var queueReaderType = typeof(ServiceBusTopicChannelReceiver<>).MakeGenericType(messageType);
        var queueReader = (IChannelReceiver)
            Activator.CreateInstance(
                queueReaderType,
                processingSettings,
                serializer,
                subscription,
                Configuration,
                configuration,
                processor
            );
        return queueReader;
    }

    public bool CanCreate(Type messageType)
    {
        return typeof(IServiceBusEvent).IsAssignableFrom(messageType);
    }
}
