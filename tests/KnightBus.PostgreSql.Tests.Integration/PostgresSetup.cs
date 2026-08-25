using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace KnightBus.PostgreSql.Tests.Integration;

[SetUpFixture]
public class PostgresSetup
{
    private static readonly PostgreSqlContainer Postgres = new PostgreSqlBuilder()
        .WithImage("postgres")
        .WithPortBinding(5433, 5432)
        .WithUsername("postgres")
        .WithPassword("passw")
        .Build();

    public static NpgsqlDataSource DataSource;
    public static string ConnectionString;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        await Postgres.StartAsync();
        DataSource = NpgsqlDataSource.Create(Postgres.GetConnectionString());
        ConnectionString = Postgres.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        await Postgres.DisposeAsync();
    }
}
