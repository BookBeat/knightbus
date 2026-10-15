using System.Linq;
using AwesomeAssertions;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Unit;

[TestFixture]
public class StorageExtensionsTests
{
    [Test]
    public void Should_register_singleton_placement_with_the_blob_lock_manager()
    {
        //act
        var services = new ServiceCollection().UseBlobStorageLockManager();

        //assert
        services.Count(x => x.ServiceType == typeof(ISingletonLockManager)).Should().Be(1);
        services.Count(x => x.ServiceType == typeof(ISingletonPlacement)).Should().Be(1);
    }

    [Test]
    public void Should_resolve_the_placement_registered_with_the_blob_lock_manager()
    {
        //arrange
        var services = new ServiceCollection()
            .UseBlobStorage("UseDevelopmentStorage=true")
            .UseBlobStorageLockManager();

        //act
        using var provider = services.BuildServiceProvider();
        var placement = provider.GetRequiredService<ISingletonPlacement>();

        //assert
        placement.HostId.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Should_keep_the_lock_manager_when_placement_is_removed()
    {
        //act
        var services = new ServiceCollection()
            .UseBlobStorageLockManager()
            .RemoveAll<ISingletonPlacement>();

        //assert
        services.Count(x => x.ServiceType == typeof(ISingletonLockManager)).Should().Be(1);
        services.Any(x => x.ServiceType == typeof(ISingletonPlacement)).Should().BeFalse();
    }
}
