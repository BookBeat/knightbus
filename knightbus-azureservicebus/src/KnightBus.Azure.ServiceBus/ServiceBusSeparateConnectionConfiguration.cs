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
        public string ConnectionString { get; set; }
        public ServiceBusCreationOptions DefaultCreationOptions { get; set; } = new ServiceBusCreationOptions();
        public IClientFactory ClientFactory => new ClientFactory(ConnectionString);
    }
}