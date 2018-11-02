using System.Collections.Generic;
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
        public IMessageAttachmentProvider AttachmentProvider { get; set; }
        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();
        public string ConnectionString { get; }
        public ServiceBusCreationOptions CreationOptions  { get; } = new ServiceBusCreationOptions();
    }
}