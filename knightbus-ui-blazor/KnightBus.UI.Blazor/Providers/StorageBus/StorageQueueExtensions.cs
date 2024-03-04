namespace KnightBus.UI.Blazor.Providers.StorageBus;

public static class StorageQueueExtensions
{
    public static IServiceCollection UseBlobStorage(this IServiceCollection services)
    {
        return services
            .AddScoped<StorageQueueManager>()
            .AddScoped<IQueueManager, StorageQueueManager>();
    }
}
