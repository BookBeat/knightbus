using KnightBus.Core;

namespace KnightBus.Cosmos;

public class CosmosTransport : ITransport
{
    public CosmosTransport(ICosmosConfiguration configuration)
    {
        TransportChannelFactories =
        [
            new CosmosSubscriptionChannelFactory(configuration)
        ];
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
