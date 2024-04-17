using Npgsql;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

[SetUpFixture]
public sealed class PostgresTestBase
{
    public static NpgsqlDataSource TestNpgsqlDataSource = null!;
    private const string ConnectionString = "Server=127.0.0.1;" +
                                            "Port=5432;" +
                                            "Database=knightbus;" +
                                            "User Id=postgres;" +
                                            "Password=passw;" +
                                            "Include Error Detail=true;";
    [OneTimeSetUp]
    public void BaseSetup()
    {
        TestNpgsqlDataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
    }
}
