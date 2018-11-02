using KnightBus.Core;

namespace KnightBus.Azure.Storage
{
    public class StorageTransport : ITransport
    {
        private readonly StorageBusConfiguration _configuration;
        public ITransportFactory[] TransportFactories { get; }
        public ITransportConfiguration Configuration => _configuration;

        public StorageTransport(string connectionString)
        {
            _configuration = new StorageBusConfiguration(connectionString);
            TransportFactories = new ITransportFactory[]{ new StorageQueueTransportFactory(_configuration), };
        }
    }
}