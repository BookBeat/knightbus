using KnightBus.PostgreSql.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.PostgreSql.Management.Extensions.Azure;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Adds a PostgreSQL management data source to the service collection, using Azure Managed Identity to acquire access tokens.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the management data source to.</param>
    /// <param name="configuration">An optional configuration action for <see cref="PostgresAzureConfiguration"/> settings.</param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection UsePostgresManagementWithAzureManagedIdentity(
        this IServiceCollection services,
        Action<PostgresAzureConfiguration>? configuration = null
    )
    {
        var postgresAzureConfiguration = new PostgresAzureConfiguration();
        configuration?.Invoke(postgresAzureConfiguration);

        return services.UsePostgresManagementWithAzureManagedIdentity(postgresAzureConfiguration);
    }

    /// <summary>
    /// Adds a PostgreSQL management data source to the service collection, using Azure Managed Identity to acquire access tokens.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the management data source to.</param>
    /// <param name="configuration"></param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection UsePostgresManagementWithAzureManagedIdentity(
        this IServiceCollection services,
        PostgresAzureConfiguration postgresAzureConfiguration
    )
    {
        return services.UsePostgresManagement(
            postgresAzureConfiguration.ToPostgresConfiguration(),
            postgresAzureConfiguration.WithManagedIdentityPasswordProvider()
        );
    }
}
