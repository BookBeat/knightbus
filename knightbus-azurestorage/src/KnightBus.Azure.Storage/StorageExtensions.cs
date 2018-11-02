using KnightBus.Azure.Storage.Singleton;
using KnightBus.Core;

namespace KnightBus.Azure.Storage
{
    public static class StorageExtensions
    {
        public static ITransport UseBlobStorageAttachments(this ITransport transport, string connectionString)
        {
            transport.Configuration.AttachmentProvider = new BlobStorageMessageAttachmentProvider(connectionString);
            return transport;
        }

        public static ITransportConfiguration UseBlobStorageAttachments(this ITransportConfiguration transportConfiguration, string connectionString)
        {
            transportConfiguration.AttachmentProvider = new BlobStorageMessageAttachmentProvider(connectionString);
            return transportConfiguration;
        }

        public static IHostConfiguration UseBlobStorageLockManager(this IHostConfiguration configuration, string connectionString)
        {
            configuration.SingletonLockManager = new BlobLockManager(connectionString);
            return configuration;
        }
    }
}