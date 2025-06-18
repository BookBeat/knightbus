using Azure.Core;
using Azure.Identity;
using KnightBus.PostgreSql.Management;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace KnightBus.PostgreSql.Extensions.Azure;

public class PostgresAzureConfiguration : PostgresConfiguration
{
    /// <summary>
    /// The time to wait between successful token refresh operations. Defaults to 55 minutes
    /// </summary>
    public TimeSpan SuccessRefreshInterval { get; set; } = TimeSpan.FromMinutes(55);

    /// <summary>
    /// The maximum duration to wait for an Azure AD token before cancelling the request. Defaults to 10 seconds
    /// </summary>
    public TimeSpan FailureRefreshInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Factory function that returns a <see cref="TokenCredential"/> to use for acquiring Azure AD tokens. Defaults to <see cref="ManagedIdentityCredential"/>
    /// </summary>
    public Func<TokenCredential> TokenCredentialFactory { get; set; } =
        () => new ManagedIdentityCredential();

    /// <summary>
    /// The set of scopes to request when acquiring an Azure AD token.
    /// </summary>
    public string[] TokenRequestScopes { get; set; } =
        ["https://ossrdbms-aad.database.windows.net/.default"];
}

public static class ServiceCollectionExtensions
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
        var postgresConfiguration = new PostgresAzureConfiguration();
        configuration?.Invoke(postgresConfiguration);

        services.UsePostgres(
            ToPostgresConfiguration(postgresConfiguration),
            WithManagedIdentityPasswordProvider(postgresConfiguration)
        );

        return services;
    }

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

        return services.UsePostgresManagement(
            ToPostgresConfiguration(postgresAzureConfiguration),
            WithManagedIdentityPasswordProvider(postgresAzureConfiguration)
        );
    }

    private static Action<IPostgresConfiguration> ToPostgresConfiguration(
        PostgresAzureConfiguration configuration
    )
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(
            configuration.ConnectionString
        )
        {
            Password = null, // overwrite password if present in connectionString, otherwise npgsql will throw
        };

        return c =>
        {
            c.PollingDelay = configuration.PollingDelay;
            c.ConnectionString = connectionStringBuilder.ToString();
            c.MessageSerializer = configuration.MessageSerializer;
        };
    }

    private static Action<NpgsqlDataSourceBuilder> WithManagedIdentityPasswordProvider(
        PostgresAzureConfiguration configuration
    )
    {
        return builder =>
        {
            builder.UsePeriodicPasswordProvider(
                // The cancellation token passed in as second arg in this delegate is only cancelled when the entire data source is disposed
                async (_, cancellationToken) =>
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken
                    );
                    cts.CancelAfter(configuration.FailureRefreshInterval);

                    var credentials = configuration.TokenCredentialFactory();
                    var token = await credentials.GetTokenAsync(
                        new TokenRequestContext(configuration.TokenRequestScopes),
                        cts.Token
                    );

                    return token.Token;
                },
                configuration.SuccessRefreshInterval,
                configuration.FailureRefreshInterval
            );
        };
    }
}
