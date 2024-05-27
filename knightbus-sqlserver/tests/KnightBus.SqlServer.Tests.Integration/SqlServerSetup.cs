using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using Testcontainers.MsSql;

namespace KnightBus.SqlServer.Tests.Integration;

[SetUpFixture]
public class SqlServerSetup
{
    private const string DatabaseName = "KnightBus";
    
    private static readonly MsSqlContainer MsSql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
        .WithPortBinding(14333, 1433)
        .Build();

    public static string ConnectionString;
    
    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        await MsSql.StartAsync();
        ConnectionString = MsSql.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        await MsSql.DisposeAsync();
    }
}
