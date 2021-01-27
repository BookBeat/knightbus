using KnightBus.Core;

namespace KnightBus.Azure.ServiceBus
{
    public class ServiceBusConfiguration : IServiceBusConfiguration
    {
        public ServiceBusConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public IMessageSerializer MessageSerializer { get; set; } = new JsonMessageSerializer();
        public string ConnectionString { get; }
        public DefaultServiceBusCreationOptions CreationOptions  { get; } = new DefaultServiceBusCreationOptions();
    }
}