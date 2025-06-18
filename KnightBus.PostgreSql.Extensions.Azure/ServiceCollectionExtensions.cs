using Azure.Core;
using Azure.Identity;
using KnightBus.PostgreSql.Management;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace KnightBus.PostgreSql.Extensions.Azure;

public interface IPostgresAzureConfiguration : IPostgresConfiguration
{
    TimeSpan SuccessRefreshInterval { get; }
    TimeSpan FailureRefreshInterval { get; }
}

public class PostgresAzureConfiguration : PostgresConfiguration, IPostgresAzureConfiguration
{
    public TimeSpan SuccessRefreshInterval { get; set; } = TimeSpan.FromMinutes(55);
    public TimeSpan FailureRefreshInterval { get; } = TimeSpan.FromSeconds(10);
}

public static class ServiceCollectionExtensions
{
    private static readonly string[] Scopes =
    [
        "https://ossrdbms-aad.database.windows.net/.default",
    ];

    public static IServiceCollection UsePostgresWithAzureManagedIdentity(
        this IServiceCollection services,
        Action<IPostgresAzureConfiguration>? configuration = null
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

    public static IServiceCollection UsePostgresManagementWithAzureManagedIdentity(
        this IServiceCollection services,
        Action<IPostgresAzureConfiguration>? configuration = null
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
        IPostgresAzureConfiguration configuration
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
        IPostgresAzureConfiguration configuration
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

                    var credentials = new DefaultAzureCredential();
                    var token = await credentials.GetTokenAsync(
                        new TokenRequestContext(Scopes),
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
