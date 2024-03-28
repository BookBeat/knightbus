using KnightBus.Core.Management;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Redis.Management;

public static class RedisQueueExtensions
{
    public static IServiceCollection UseRedisManagement(this IServiceCollection services, string connectionString, int databaseId = 0)
    {
        services = services
            .AddScoped<RedisQueueManager>()
            .AddScoped<IQueueManager, RedisQueueManager>()
            .AddScoped<IRedisManagementClient, RedisManagementClient>();

        return services.UseRedis(c =>
        {
            c.ConnectionString = connectionString;
            c.DatabaseId = databaseId;
        });
    }
}
