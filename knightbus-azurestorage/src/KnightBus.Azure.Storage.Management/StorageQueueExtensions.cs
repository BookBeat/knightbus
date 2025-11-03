using System;
using KnightBus.Core.Management;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Azure.Storage.Management;

public static class StorageQueueExtensions
{
    public static IServiceCollection UseBlobStorageManagement(
        this IServiceCollection services,
        string connectionString
    )
    {
        return services.UseBlobStorageManagement(config =>
        {
            config.ConnectionString = connectionString;
        });
    }

    public static IServiceCollection UseBlobStorageManagement(
        this IServiceCollection services,
        Action<IStorageBusConfiguration> config
    )
    {
        return services.UseBlobStorageManagement(_ =>
        {
            var storageBusConfiguration = new StorageBusConfiguration();
            config.Invoke(storageBusConfiguration);
            return storageBusConfiguration;
        });
    }

    public static IServiceCollection UseBlobStorageManagement(
        this IServiceCollection services,
        Func<IServiceProvider, IStorageBusConfiguration> configFactory
    )
    {
        services = services
            .AddScoped<StorageQueueManager>()
            .AddScoped<IQueueManager, StorageQueueManager>()
            .AddScoped<IQueueMessageAttachmentProvider, StorageQueueManager>()
            .AddSingleton<BlobStorageMessageAttachmentProvider>();

        return services.UseBlobStorage(configFactory);
    }
}
