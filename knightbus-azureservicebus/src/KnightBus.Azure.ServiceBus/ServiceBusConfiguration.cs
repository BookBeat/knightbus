using KnightBus.Messages;
using KnightBus.Newtonsoft;

namespace KnightBus.Azure.ServiceBus
{
    public class ServiceBusConfiguration : IServiceBusConfiguration
    {
        public ServiceBusConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
            ClientFactory = new ClientFactory(connectionString);
        }
        public IMessageSerializer MessageSerializer { get; set; } = new NewtonsoftSerializer();
        public string ConnectionString { get; }
        public ServiceBusCreationOptions DefaultCreationOptions { get; set; } = new ServiceBusCreationOptions();
        public IClientFactory ClientFactory { get; }
    }
}