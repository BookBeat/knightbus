using KnightBus.Core.Sagas;
using KnightBus.PostgreSql.Sagas;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace KnightBus.PostgreSql;

public static class PostgresExtensions
{
    public static IServiceCollection UsePostgres(
        this IServiceCollection services,
        Action<IPostgresConfiguration>? configuration = null,
        Action<NpgsqlDataSourceBuilder>? dataSourceBuilder = null
    )
    {
        var postgresConfiguration = new PostgresConfiguration();
        configuration?.Invoke(postgresConfiguration);
        services.AddSingleton<IPostgresConfiguration>(_ => postgresConfiguration);

        services.AddNpgsqlDataSource(
            postgresConfiguration.ConnectionString,
            dataSourceBuilder ?? (_ => { }),
            serviceKey: PostgresConstants.NpgsqlDataSourceContainerKey
        );
        services.AddScoped<IPostgresBus, PostgresBus>();
        return services;
    }

    public static IServiceCollection UsePostgresSagaStore(this IServiceCollection services)
    {
        services.EnableSagas<PostgresSagaStore>();
        return services;
    }
}
