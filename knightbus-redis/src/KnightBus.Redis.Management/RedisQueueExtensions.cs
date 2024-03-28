using KnightBus.Core.Management;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Redis.Management;

public static class RedisQueueExtensions
{
    public static IServiceCollection UseRedisManagement(this IServiceCollection services, string connectionString)
    {
        services = services
            .AddScoped<RedisQueueManager>()
            .AddScoped<IQueueManager, RedisQueueManager>();

        return services.UseRedis(c => c.ConnectionString = connectionString);
    }
}
