using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace KnightBus.PostgreSql.Extensions.Azure;

public class PostgresAzureConfiguration : PostgresConfiguration
{
    /// <summary>
    /// The time to wait between successful token refresh operations.
    /// <remarks>Defaults to 55 minutes</remarks>
    /// </summary>
    public TimeSpan SuccessRefreshInterval { get; set; } = TimeSpan.FromMinutes(55);

    /// <summary>
    /// The maximum duration to wait for an Azure AD token before cancelling the request.
    /// <remarks>Defaults to 10 seconds</remarks>
    /// </summary>
    public TimeSpan RefreshTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// If a password refresh attempt fails, it will be re-attempted with this interval. This should typically be much lower than <see cref="SuccessRefreshInterval"/>.
    /// <remarks>Defaults to 10 seconds</remarks>
    /// </summary>
    public TimeSpan FailureRefreshInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// <see cref="TokenCredential"/> to use for acquiring Azure AD tokens. Defaults to <see cref="ManagedIdentityCredential"/>
    /// </summary>
    public TokenCredential TokenCredential { get; set; } = new ManagedIdentityCredential();

    /// <summary>
    /// The set of scopes to request when acquiring an Azure AD token.
    /// </summary>
    public string[] TokenRequestScopes { get; set; } =
    ["https://ossrdbms-aad.database.windows.net/.default"];
}

public static class PostgresAzureConfigurationExtensions
{
    public static PostgresAzureConfiguration RemovePasswordFromConnectionString(
        this PostgresAzureConfiguration postgresAzureConfiguration
    )
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(
            postgresAzureConfiguration.ConnectionString
        )
        {
            Password = null, // overwrite password if present in connectionString, otherwise Npgsql will throw
        };

        postgresAzureConfiguration.ConnectionString = connectionStringBuilder.ToString();

        return postgresAzureConfiguration;
    }

    public static NpgsqlDataSourceBuilder WithManagedIdentityPasswordProvider(
        this PostgresAzureConfiguration configuration,
        NpgsqlDataSourceBuilder builder
    )
    {
        return builder.UsePeriodicPasswordProvider(
            // The cancellation token passed in as second arg in this delegate is only cancelled when the entire data source is disposed
            async (_, cancellationToken) =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(configuration.RefreshTimeout);

                var token = await configuration
                    .TokenCredential.GetTokenAsync(
                        new TokenRequestContext(configuration.TokenRequestScopes),
                        cts.Token
                    )
                    .ConfigureAwait(false);

                return token.Token;
            },
            configuration.SuccessRefreshInterval,
            configuration.FailureRefreshInterval
        );
    }
}
