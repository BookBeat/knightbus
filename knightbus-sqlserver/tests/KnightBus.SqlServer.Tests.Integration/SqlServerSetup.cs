using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using NUnit.Framework;
using Testcontainers.MsSql;

namespace KnightBus.SqlServer.Tests.Integration;

[SetUpFixture]
public class SqlServerSetup
{
    private const string DatabaseName = "KnightBus";

    private static readonly IDatabaseContainer MsSql = new MsSqlBuilder()
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
