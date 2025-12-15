using System.Data;
using AwesomeAssertions;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace KnightBus.PostgreSql.Extensions.Azure.Tests.Integration;

public class IServiceCollectionExtensionsTests
{
    private const string Password = "password";
    private static readonly PostgreSqlContainer PostgresContainer = new PostgreSqlBuilder()
        .WithPortBinding(5432, true)
        .WithPassword(Password)
        .Build();

    private static string _connectionString;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        await PostgresContainer.StartAsync();
        _connectionString = new NpgsqlConnectionStringBuilder(
            PostgresContainer.GetConnectionString()
        )
        {
            Password = null,
        }.ToString();
    }

    [OneTimeTearDown]
    public async Task OneTimeTeardown()
    {
        await PostgresContainer.DisposeAsync();
    }

    [Test]
    public async Task UsePostgresWithAzureManagedIdentity_WithFunc_RegistersCorrectly()
    {
        // Arrange
        var target = new ServiceCollection();
        target.AddSingleton<TokenCredential>(new MockCredential());

        // Act
        target.UsePostgresWithAzureManagedIdentity(provider => new PostgresAzureConfiguration
        {
            TokenCredential = provider.GetRequiredService<TokenCredential>(),
            ConnectionString = _connectionString,
        });
        var serviceProvider = target.BuildServiceProvider();

        // Assert
        var result = serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(
            PostgresConstants.NpgsqlDataSourceContainerKey
        );
        await using var connection = await result.OpenConnectionAsync();
        connection.State.Should().Be(ConnectionState.Open);
    }

    [Test]
    public async Task UsePostgresWithAzureManagedIdentity_WithAction_RegistersCorrectly()
    {
        // Arrange
        var target = new ServiceCollection();

        // Act
        target.UsePostgresWithAzureManagedIdentity(x =>
        {
            x.TokenCredential = new MockCredential();
            x.ConnectionString = _connectionString;
        });
        var serviceProvider = target.BuildServiceProvider();

        // Assert
        var result = serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(
            PostgresConstants.NpgsqlDataSourceContainerKey
        );
        await using var connection = await result.OpenConnectionAsync();
        connection.State.Should().Be(ConnectionState.Open);
    }

    [Test]
    public async Task UsePostgresWithAzureManagedIdentity_WithConfiguration_RegistersCorrectly()
    {
        // Arrange
        var target = new ServiceCollection();

        // Act
        target.UsePostgresWithAzureManagedIdentity(
            new PostgresAzureConfiguration
            {
                TokenCredential = new MockCredential(),
                ConnectionString = _connectionString,
            }
        );
        var serviceProvider = target.BuildServiceProvider();

        // Assert
        var result = serviceProvider.GetRequiredKeyedService<NpgsqlDataSource>(
            PostgresConstants.NpgsqlDataSourceContainerKey
        );
        await using var connection = await result.OpenConnectionAsync();
        connection.State.Should().Be(ConnectionState.Open);
    }

    private class MockCredential : TokenCredential
    {
        private static AccessToken Create() =>
            new AccessToken(Password, DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken
        ) => new(Create());

        public override AccessToken GetToken(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken
        ) => Create();
    }
}
