using KnightBus.Azure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.UI.Console.Providers.StorageBus;

public static class StorageQueueExtensions
{
    public static IServiceCollection UseBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection(StorageConnectionConfig.SectionName).Get<StorageConnectionConfig>();
        if (string.IsNullOrEmpty(config?.ConnectionString))
            return services;

        services.AddSingleton<IQueueManager, StorageQueueManager>();
        return services.UseBlobStorage(c => c.ConnectionString = config.ConnectionString);
    }
}
