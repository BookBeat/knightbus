using KnightBus.Core;

namespace KnightBus.Azure.Storage
{
    public class StorageTransport : ITransport
    {
        private readonly StorageBusConfiguration _configuration;
        public ITransportChannelFactory[] TransportChannelFactories { get; }
        public ITransport ConfigureChannels(ITransportConfiguration configuration)
        {
            throw new System.NotImplementedException();
        }

        public ITransport UseMiddleware(IMessageProcessorMiddleware middleware)
        {
            throw new System.NotImplementedException();
        }

        public ITransportConfiguration Configuration => _configuration;

        public StorageTransport(string connectionString)
        {
            _configuration = new StorageBusConfiguration(connectionString);
            TransportChannelFactories = new ITransportChannelFactory[]{ new StorageQueueTransportFactory(_configuration), };
        }
    }
}