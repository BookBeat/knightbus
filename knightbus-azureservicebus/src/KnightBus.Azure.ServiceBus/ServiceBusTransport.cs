using KnightBus.Core;

namespace KnightBus.Azure.ServiceBus;

public class ServiceBusTransport : ITransport
{
    public ServiceBusTransport(string connectionString) : this(new ServiceBusConfiguration(connectionString))
    {
    }

    public ServiceBusTransport(IServiceBusConfiguration configuration)
    {
        TransportChannelFactories = new ITransportChannelFactory[]
        {
            new ServiceBusQueueChannelFactory(configuration),
            new ServiceBusTopicChannelFactory(configuration)
        };
    }

    public ITransport ConfigureChannels(ITransportConfiguration configuration)
    {
        foreach (var channelFactory in TransportChannelFactories)
        {
            channelFactory.Configuration = configuration;
        }

        return this;
    }
    public ITransportChannelFactory[] TransportChannelFactories { get; }
}
