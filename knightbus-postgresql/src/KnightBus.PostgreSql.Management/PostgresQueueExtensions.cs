using KnightBus.Core.Management;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.PostgreSql.Management;

public static class PostgresQueueExtensions
{
    public static IServiceCollection UsePostgresManagement(
        this IServiceCollection services,
        string connectionString
    )
    {
        services = services
            .AddScoped<PostgresQueueManager>()
            .AddScoped<PostgresTopicManager>()
            .AddScoped<IQueueManager, PostgresQueueManager>()
            .AddScoped<IQueueMessageSender, PostgresQueueManager>()
            .AddScoped<IQueueManager, PostgresTopicManager>()
            .AddScoped<PostgresManagementClient>();

        return services.UsePostgres(c =>
        {
            c.ConnectionString = connectionString;
        });
    }
}
