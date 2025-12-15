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
    /// <param name="configure">A configuration action for <see cref="PostgresAzureConfiguration"/> settings that has access to the current <see cref="IServiceProvider"/>.</param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection UsePostgresManagementWithAzureManagedIdentity(
        this IServiceCollection services,
        Func<IServiceProvider, PostgresAzureConfiguration> configure
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.UsePostgresManagement(
            sp =>
            {
                return configure(sp).RemovePasswordFromConnectionString();
            },
            (sp, builder) =>
            {
                if (
                    sp.GetRequiredService<IPostgresConfiguration>()
                    is not PostgresAzureConfiguration azureConfiguration
                )
                {
                    throw new InvalidOperationException(
                        $"The registered Postgres configuration must be of type {nameof(PostgresAzureConfiguration)}."
                    );
                }

                azureConfiguration.WithManagedIdentityPasswordProvider(builder);
            }
        );
    }

    /// <summary>
    /// Adds a PostgreSQL management data source to the service collection, using Azure Managed Identity to acquire access tokens.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the management data source to.</param>
    /// <param name="configuration"></param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection UsePostgresManagementWithAzureManagedIdentity(
        this IServiceCollection services,
        PostgresAzureConfiguration azureConfiguration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(azureConfiguration);

        return services.UsePostgresManagement(
            _ => azureConfiguration.RemovePasswordFromConnectionString(),
            (_, builder) => azureConfiguration.WithManagedIdentityPasswordProvider(builder)
        );
    }
}
