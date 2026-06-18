using KnightBus.Core;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ;

public class LavinMQTransport : ITransport
{
    public LavinMQTransport(ILavinMQConfiguration configuration, IConnection connection)
    {
        TransportChannelFactories = new ITransportChannelFactory[]
        {
            new LavinMQChannelFactory(configuration, connection),
        };
    }

    public ITransportChannelFactory[] TransportChannelFactories { get; }

    public ITransport ConfigureChannels(ITransportConfiguration configuration)
    {
        foreach (var channelFactory in TransportChannelFactories)
        {
            channelFactory.Configuration = configuration;
        }

        return this;
    }
}
