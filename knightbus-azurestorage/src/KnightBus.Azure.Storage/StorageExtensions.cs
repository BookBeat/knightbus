using KnightBus.Azure.Storage.Singleton;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;

namespace KnightBus.Azure.Storage
{
    public static class StorageExtensions
    {
        public static ITransport UseBlobStorageAttachments(this ITransport transport, string connectionString)
        {
            transport.UseMiddleware(new AttachmentMiddleware(new BlobStorageMessageAttachmentProvider(connectionString)));
            return transport;
        }

        public static IHostConfiguration UseBlobStorageAttachments(this IHostConfiguration configuration, string connectionString)
        {
            configuration.Middlewares.Add(new AttachmentMiddleware(new BlobStorageMessageAttachmentProvider(connectionString)));
            return configuration;
        }

        public static IHostConfiguration UseBlobStorageLockManager(this IHostConfiguration configuration, string connectionString)
        {
            configuration.SingletonLockManager = new BlobLockManager(connectionString);
            return configuration;
        }
    }
}