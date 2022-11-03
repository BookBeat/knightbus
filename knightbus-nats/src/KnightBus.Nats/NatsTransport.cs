using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using NATS.Client;

namespace KnightBus.Nats
{
    public class NatsTransport : ITransport
    {
        public NatsTransport(string connectionString)
            : this(new NatsBusConfiguration(connectionString))
        {
            
        }
        public NatsTransport(INatsBusConfiguration configuration)
        {
            TransportChannelFactories = new ITransportChannelFactory[] {new NatsChannelFactory(configuration),};
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

        public ITransport UseMiddleware(IMessageProcessorMiddleware middleware)
        {
            foreach (var channelFactory in TransportChannelFactories)
            {
                channelFactory.Middlewares.Add(middleware);
            }

            return this;
        }
    }

    public interface INatsBusConfiguration : ITransportConfiguration
    {
        public Options Options { get; }
    }

    public class NatsBusConfiguration : INatsBusConfiguration
    {
        public NatsBusConfiguration(string connectionString)
        {
            Options = ConnectionFactory.GetDefaultOptions();
            Options.Url = connectionString;
            ConnectionString = connectionString;
        }
        public string ConnectionString { get; }
        public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
        public Options Options { get; }
    }
}