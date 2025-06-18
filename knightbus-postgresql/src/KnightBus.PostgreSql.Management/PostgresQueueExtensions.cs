using KnightBus.Core.Management;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace KnightBus.PostgreSql.Management;

public static class PostgresQueueExtensions
{
    public static IServiceCollection UsePostgresManagement(
        this IServiceCollection services,
        string connectionString
    )
    {
        return UsePostgresManagement(
            services,
            c =>
            {
                c.ConnectionString = connectionString;
            }
        );
    }

    public static IServiceCollection UsePostgresManagement(
        this IServiceCollection services,
        Action<IPostgresConfiguration>? configuration = null,
        Action<NpgsqlDataSourceBuilder>? dataSourceBuilder = null
    )
    {
        services = services
            .AddScoped<PostgresQueueManager>()
            .AddScoped<PostgresTopicManager>()
            .AddScoped<IQueueManager, PostgresQueueManager>()
            .AddScoped<IQueueMessageSender, PostgresQueueManager>()
            .AddScoped<IQueueManager, PostgresTopicManager>()
            .AddScoped<PostgresManagementClient>();

        return services.UsePostgres(configuration, dataSourceBuilder);
    }
}
