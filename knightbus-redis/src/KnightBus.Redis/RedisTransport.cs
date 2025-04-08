using KnightBus.Core;
using StackExchange.Redis;

namespace KnightBus.Redis;

public class RedisTransport : ITransport
{
    public RedisTransport(string connectionString)
        : this(new RedisConfiguration(connectionString)) { }

    public RedisTransport(IRedisConfiguration configuration)
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
}
