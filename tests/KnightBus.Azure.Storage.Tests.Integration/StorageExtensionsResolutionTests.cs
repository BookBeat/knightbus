using AwesomeAssertions;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration;

[TestFixture]
public class StorageExtensionsResolutionTests
{
    [Test]
    public void Should_resolve_the_placement_registered_with_the_blob_lock_manager()
    {
        //arrange
        var services = new ServiceCollection()
            .AddLogging()
            .UseBlobStorage(StorageSetup.ConnectionString)
            .UseBlobStorageLockManager();

        //act
        using var provider = services.BuildServiceProvider();
        var placement = provider.GetRequiredService<ISingletonPlacement>();

        //assert
        placement.HostId.Should().NotBeNullOrWhiteSpace();
    }
}
