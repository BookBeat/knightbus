using KnightBus.Core;

namespace KnightBus.Azure.ServiceBus
{
    public class ServiceBusConfiguration : IServiceBusConfiguration
    {
        public ServiceBusConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public IMessageSerializer MessageSerializer { get; set; } = new MicrosoftJsonSerializer();
        public string ConnectionString { get; }
        public ServiceBusCreationOptions DefaultCreationOptions  { get; } = new ServiceBusCreationOptions();
    }
}