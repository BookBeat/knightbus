using KnightBus.Messages;
using KnightBus.Newtonsoft;

namespace KnightBus.Azure.ServiceBus
{
    public class ServiceBusSeparateConnectionConfiguration : IServiceBusConfiguration
    {
        public ServiceBusSeparateConnectionConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
        public string ConnectionString { get; }
        public ServiceBusCreationOptions DefaultCreationOptions { get; } = new ServiceBusCreationOptions();
        public IClientFactory ClientFactory => new ClientFactory(ConnectionString);
    }
}