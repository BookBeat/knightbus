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
        ArgumentNullException.ThrowIfNull(services);

        return services.UsePostgres(
            _ =>
            {
                var postgresConfiguration = new PostgresConfiguration();
                configuration?.Invoke(postgresConfiguration);
                return postgresConfiguration;
            },
            dataSourceBuilder is null ? null : (_, builder) => dataSourceBuilder(builder)
        );
    }

    public static IServiceCollection UsePostgres(
        this IServiceCollection services,
        Func<IServiceProvider, IPostgresConfiguration> configurationFactory,
        Action<IServiceProvider, NpgsqlDataSourceBuilder>? dataSourceBuilder = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationFactory);

        services.AddSingleton<IPostgresConfiguration>(sp =>
        {
            var configuration = configurationFactory(sp);
            return configuration
                ?? throw new InvalidOperationException(
                    $"{nameof(configurationFactory)} must not return null."
                );
        });

        services.AddKeyedSingleton<NpgsqlDataSource>(
            PostgresConstants.NpgsqlDataSourceContainerKey,
            (sp, _) =>
            {
                var configuration = sp.GetRequiredService<IPostgresConfiguration>();
                ArgumentException.ThrowIfNullOrWhiteSpace(configuration.ConnectionString);

                var builder = new NpgsqlDataSourceBuilder(configuration.ConnectionString);
                dataSourceBuilder?.Invoke(sp, builder);
                return builder.Build();
            }
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
