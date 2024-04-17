using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace KnightBus.PostgreSql;

public static class PostgresExtensions
{
    public static IServiceCollection UsePostgres(
        this IServiceCollection services,
        Action<IPostgresConfiguration>? configuration = null)
    {
        var postgresConfiguration = new PostgresConfiguration();
        configuration?.Invoke(postgresConfiguration);
        services.AddSingleton<IPostgresConfiguration>(_ => postgresConfiguration);
        services.AddSingleton(NpgsqlDataSource.Create(postgresConfiguration.ConnectionString ??
                                                      throw new ArgumentException(
                                                          nameof(postgresConfiguration.ConnectionString))));
        services.AddScoped<IPostgresBus, PostgresBus>();
        return services;
    }
}
