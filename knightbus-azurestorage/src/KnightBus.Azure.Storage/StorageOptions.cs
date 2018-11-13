using System.Collections.Generic;
using KnightBus.Core;

namespace KnightBus.Azure.Storage
{
    public interface IStorageBusConfiguration : ITransportConfiguration
    {

    }

    public class StorageBusConfiguration : IStorageBusConfiguration
    {
        public StorageBusConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public string ConnectionString { get; }
        public IMessageSerializer MessageSerializer { get; set; } = new JsonMessageSerializer();
        public IMessageAttachmentProvider AttachmentProvider { get; set; }
        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();
    }
}