using Azure.Storage.Queues;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage
{
    public interface IStorageBusConfiguration : ITransportConfiguration
    {
        /// <summary>
        /// Specifies if the bus should base64 encode or leave it up to the client. Base64 is mandatory for legacy.
        /// </summary>
        QueueMessageEncoding MessageEncoding { get; }
    }

    public class StorageBusConfiguration : IStorageBusConfiguration
    {
        public StorageBusConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public string ConnectionString { get; }
        public IMessageSerializer MessageSerializer { get; set; } = new MicrosoftJsonSerializer();
        /// <summary>
        /// Specifies if the bus should base64 encode or leave it up to the client. Base64 is mandatory for legacy.
        /// </summary>
        public QueueMessageEncoding MessageEncoding { get; set; } = QueueMessageEncoding.Base64;
    }
}