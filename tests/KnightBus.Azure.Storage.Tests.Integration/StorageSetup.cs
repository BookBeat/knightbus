using System.Threading.Tasks;
using NUnit.Framework;
using Testcontainers.Azurite;

namespace KnightBus.Azure.Storage.Tests.Integration;

[SetUpFixture]
internal class StorageSetup
{
    // The image was the module default until Testcontainers made it explicit; keeping the same tag.
    private static readonly AzuriteContainer Azurite = new AzuriteBuilder(
        "mcr.microsoft.com/azure-storage/azurite:3.28.0"
    )
        .WithCommand("--skipApiVersionCheck")
        .Build();
    public static string ConnectionString = null!;

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
