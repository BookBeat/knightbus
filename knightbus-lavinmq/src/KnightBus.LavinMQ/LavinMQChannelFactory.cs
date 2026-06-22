using System;
using KnightBus.Core;
using KnightBus.LavinMQ.Messages;
using KnightBus.Messages;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ;

public class LavinMQChannelFactory : ITransportChannelFactory
{
    private readonly IConnection _connection;

    public LavinMQChannelFactory(ILavinMQConfiguration configuration, IConnection connection)
    {
        Configuration = configuration;
        _connection = connection;
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
        var readerType = typeof(LavinMQChannelReceiver<>).MakeGenericType(messageType);
        var reader = (IChannelReceiver)
            Activator.CreateInstance(
                readerType,
                processingSettings,
                serializer,
                configuration,
                processor,
                (ILavinMQConfiguration)Configuration,
                _connection,
                subscription
            )!;

        return reader;
    }

    public bool CanCreate(Type messageType)
    {
        return typeof(ILavinMQCommand).IsAssignableFrom(messageType)
            || typeof(ILavinMQEvent).IsAssignableFrom(messageType);
    }
}
