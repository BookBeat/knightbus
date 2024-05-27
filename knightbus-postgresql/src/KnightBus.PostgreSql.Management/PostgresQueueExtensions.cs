using KnightBus.Core.Management;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.PostgreSql.Management;

public static class PostgresQueueExtensions
{
    public static IServiceCollection UsePostgresManagement(
        this IServiceCollection services, string connectionString)
    {
        services = services
            .AddScoped<PostgresQueueManager>()
            .AddScoped<PostgresSubscriptionManager>()
            .AddScoped<IQueueManager, PostgresQueueManager>()
            .AddScoped<IQueueManager, PostgresSubscriptionManager>()
            .AddScoped<PostgresManagementClient>();

        return services.UsePostgres(c =>
        {
            c.ConnectionString = connectionString;
        });
    }
}
