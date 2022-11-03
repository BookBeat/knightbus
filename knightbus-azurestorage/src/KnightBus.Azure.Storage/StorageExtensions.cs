using KnightBus.Azure.Storage.Singleton;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.DependencyInjection;

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

        public static IServiceCollection UseBlobStorageLockManager(this IServiceCollection collection, string connectionString)
        {
            collection.AddSingleton<ISingletonLockManager>(_ => new BlobLockManager(connectionString));
            return collection;
        }
        public static IServiceCollection UseBlobStorageAttachments(this IServiceCollection collection, string connectionString)
        {
            collection.AddSingleton<IMessageAttachmentProvider>(_ => new BlobStorageMessageAttachmentProvider(connectionString));
            return collection;
        }
        public static IServiceCollection UseBlobStorageLockManager(this IServiceCollection collection, string connectionString, IBlobLockScheme lockScheme)
        {
            collection.AddSingleton<ISingletonLockManager>(_ => new BlobLockManager(connectionString, lockScheme));
            return collection;
        }
    }
}