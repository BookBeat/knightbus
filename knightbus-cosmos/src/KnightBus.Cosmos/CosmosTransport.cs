using KnightBus.Core;
using Microsoft.Azure.Cosmos;

namespace KnightBus.Cosmos;

public class CosmosTransport : ITransport
{
    public CosmosTransport(CosmosClient cosmosClient, ICosmosConfiguration configuration)
    {
        TransportChannelFactories =
        [
            new CosmosSubscriptionChannelFactory(cosmosClient, configuration),
            new CosmosCommandChannelFactory(cosmosClient, configuration),
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
