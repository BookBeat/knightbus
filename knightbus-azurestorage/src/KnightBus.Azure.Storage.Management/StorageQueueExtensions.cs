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
        services = services
            .AddScoped<StorageQueueManager>()
            .AddScoped<IQueueManager, StorageQueueManager>();

        return StorageExtensions.UseBlobStorage(services, connectionString);
    }
}
