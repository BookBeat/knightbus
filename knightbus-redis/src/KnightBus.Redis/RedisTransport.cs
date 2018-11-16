using KnightBus.Core;

namespace KnightBus.Redis
{
    public class RedisTransport : ITransport
    {
        public RedisTransport(string connectionString) : this(new RedisConfiguration(connectionString))
        { }
        public RedisTransport(RedisConfiguration configuration)
        {
            TransportChannelFactories = new ITransportChannelFactory[]
            {
                new RedisChannelFactory(configuration)
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

        public ITransport UseMiddleware(IMessageProcessorMiddleware middleware)
        {
            foreach (var channelFactory in TransportChannelFactories)
            {
                channelFactory.Middlewares.Add(middleware);
            }

            return this;
        }
    }
}