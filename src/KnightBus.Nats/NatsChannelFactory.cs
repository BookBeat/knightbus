using System;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Nats.Messages;

namespace KnightBus.Nats;

public class NatsChannelFactory : ITransportChannelFactory
{
    public NatsChannelFactory(INatsConfiguration configuration)
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
        var readerType = typeof(NatsQueueChannelReceiver<>).MakeGenericType(messageType);
        var reader =
            Activator.CreateInstance(
                readerType,
                processingSettings,
                serializer,
                configuration,
                processor,
                Configuration,
                subscription
            ) as IChannelReceiver;

        return reader
            ?? throw new InvalidOperationException("ChannelReceiver could not be created");
    }

    public bool CanCreate(Type messageType)
    {
        return typeof(INatsCommand).IsAssignableFrom(messageType)
            || typeof(INatsEvent).IsAssignableFrom(messageType)
            || typeof(INatsRequest).IsAssignableFrom(messageType);
        ;
    }
}
