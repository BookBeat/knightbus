using KnightBus.Core;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public class RedisTransport : ITransport
    {
        public RedisTransport(string connectionString) : this(new RedisConfiguration(connectionString))
        { }
        public RedisTransport(RedisConfiguration configuration)
        {
            var multiplexer = ConnectionMultiplexer.Connect(configuration.ConnectionString);
            TransportChannelFactories = new ITransportChannelFactory[]
            {
                new RedisCommandChannelFactory(configuration, multiplexer),
                new RedisEventChannelFactory(configuration, multiplexer), 
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