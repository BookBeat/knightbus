using System.Threading.Tasks;
using NUnit.Framework;
using Testcontainers.SqlEdge;

namespace KnightBus.SqlServer.Tests.Integration;

[SetUpFixture]
public class SqlServerSetup
{
    private const string DatabaseName = "KnightBus";

    private static readonly SqlEdgeContainer MsSql = new SqlEdgeBuilder()
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
