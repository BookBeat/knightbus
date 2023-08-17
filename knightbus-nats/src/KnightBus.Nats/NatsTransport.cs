using KnightBus.Core;

namespace KnightBus.Nats
{
    public class NatsTransport : ITransport
    {
        public NatsTransport(string connectionString) : this(new NatsConfiguration { ConnectionString = connectionString })
        {

        }
        public NatsTransport(INatsConfiguration configuration)
        {
            TransportChannelFactories = new ITransportChannelFactory[] { new NatsChannelFactory(configuration), };
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

    public class JetStreamTransport : ITransport
    {
        public JetStreamTransport(string connectionString) : this(new JetStreamConfiguration { ConnectionString = connectionString })
        {

        }
        public JetStreamTransport(IJetStreamConfiguration configuration)
        {
            TransportChannelFactories = new ITransportChannelFactory[] { new NatsChannelFactory(configuration), };
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
}
