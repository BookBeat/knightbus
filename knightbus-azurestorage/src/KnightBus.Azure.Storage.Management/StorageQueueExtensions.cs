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
        services = services
            .AddScoped<StorageQueueManager>()
            .AddScoped<IQueueManager, StorageQueueManager>()
            .AddScoped<IQueueMessageAttachmentProvider, StorageQueueManager>()
            .AddSingleton<BlobStorageMessageAttachmentProvider>();

        return StorageExtensions.UseBlobStorage(services, config);
    }
}
