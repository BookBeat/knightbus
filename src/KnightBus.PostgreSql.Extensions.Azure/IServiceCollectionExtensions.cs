using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.PostgreSql.Extensions.Azure;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds a PostgreSQL data source to the service collection, using Azure Managed Identity to acquire access tokens.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the PostgreSQL data source to.</param>
    /// <param name="configuration">An optional configuration action for <see cref="PostgresAzureConfiguration"/> settings.</param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection UsePostgresWithAzureManagedIdentity(
        this IServiceCollection services,
        Action<PostgresAzureConfiguration>? configuration = null
    )
    {
        var postgresAzureConfiguration = new PostgresAzureConfiguration();
        configuration?.Invoke(postgresAzureConfiguration);

        return services.UsePostgresWithAzureManagedIdentity(postgresAzureConfiguration);
    }

    /// <summary>
    /// Adds a PostgreSQL data source to the service collection, using Azure Managed Identity to acquire access tokens.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the PostgreSQL data source to.</param>
    /// <param name="configuration"></param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection UsePostgresWithAzureManagedIdentity(
        this IServiceCollection services,
        PostgresAzureConfiguration configuration
    )
    {
        return services.UsePostgres(
            configuration.ToPostgresConfiguration(),
            configuration.WithManagedIdentityPasswordProvider()
        );
    }
}
