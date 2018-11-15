using KnightBus.Core;

namespace KnightBus.Azure.Storage
{
    public class StorageTransport : ITransport
    {
        public StorageTransport(string connectionString):this(new StorageBusConfiguration(connectionString))
        {}

        public StorageTransport(IStorageBusConfiguration configuration)
        {
            TransportChannelFactories = new ITransportChannelFactory[]{new StorageQueueChannelFactory(configuration), };
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