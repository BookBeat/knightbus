using System.Threading.Tasks;
using NUnit.Framework;
using Testcontainers.Azurite;

namespace KnightBus.Azure.Storage.Tests.Integration;

[SetUpFixture]
public class AzuriteSetup
{
    public static readonly AzuriteContainer AzuriteContainer = new AzuriteBuilder().WithCommand("--skipApiVersionCheck").Build();
    public static string ConnectionString;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        await AzuriteContainer.StartAsync();
        ConnectionString = AzuriteContainer.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task OneTimeTeardown()
    {
        await AzuriteContainer.DisposeAsync();
    }
}
