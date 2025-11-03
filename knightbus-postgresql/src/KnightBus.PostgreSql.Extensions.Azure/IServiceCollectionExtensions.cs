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
    /// <param name="configure">A configuration action for <see cref="PostgresAzureConfiguration"/> that has access to the current <see cref="IServiceProvider"/>.</param>
    /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection UsePostgresWithAzureManagedIdentity(
        this IServiceCollection services,
        Func<IServiceProvider, PostgresAzureConfiguration> configure
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.UsePostgres(
            sp =>
            {
                var configuration = configure(sp);
                configuration.ToPostgresConfiguration()(configuration);
                return configuration;
            },
            (sp, builder) =>
            {
                if (
                    sp.GetRequiredService<IPostgresConfiguration>()
                    is not PostgresAzureConfiguration azureConfiguration
                )
                {
                    throw new InvalidOperationException(
                        "The registered Postgres configuration must be of type PostgresAzureConfiguration."
                    );
                }

                azureConfiguration.WithManagedIdentityPasswordProvider()(builder);
            }
        );
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
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.ToPostgresConfiguration()(configuration);

        return services.UsePostgres(
            _ => configuration,
            (_, builder) => configuration.WithManagedIdentityPasswordProvider()(builder)
        );
    }
}
