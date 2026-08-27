using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using NUnit.Framework;
using Testcontainers.MsSql;

namespace KnightBus.SqlServer.Tests.Integration;

[SetUpFixture]
public class SqlServerSetup
{
    private const string DatabaseName = "KnightBus";

    // The image was the module default until Testcontainers made it explicit; keeping the same tag.
    private static readonly IDatabaseContainer MsSql = new MsSqlBuilder(
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04"
    )
        .WithPortBinding(14333, 1433)
        .Build();

    public static string ConnectionString = null!;

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
