using KnightBus.Core;

namespace KnightBus.PostgreSql;

public class PostgresTransport : ITransport
{
    public PostgresTransport(IPostgresConfiguration postgresConfiguration)
    {
        TransportChannelFactories = [new PostgresChannelFactory(postgresConfiguration)];
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
