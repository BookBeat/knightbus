using System.Threading.Tasks;
using NUnit.Framework;
using Testcontainers.Azurite;

namespace KnightBus.Azure.Storage.Tests.Integration;

[SetUpFixture]
internal class StorageSetup
{
    private static readonly AzuriteContainer Azurite = new AzuriteBuilder().WithCommand("--skipApiVersionCheck").Build();
    public static string ConnectionString;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await Azurite.StartAsync();
        ConnectionString = Azurite.GetConnectionString();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await Azurite.DisposeAsync();
    }
}
