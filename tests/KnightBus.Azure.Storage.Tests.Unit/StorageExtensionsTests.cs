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
    public void Should_register_singleton_membership_with_the_blob_lock_manager()
    {
        //act
        var services = new ServiceCollection().UseBlobStorageLockManager();

        //assert
        services.Count(x => x.ServiceType == typeof(ISingletonLockManager)).Should().Be(1);
        services.Count(x => x.ServiceType == typeof(ISingletonMembership)).Should().Be(1);
    }

    [Test]
    public void Should_resolve_the_membership_registered_with_the_blob_lock_manager()
    {
        //arrange
        var services = new ServiceCollection()
            .UseBlobStorage("UseDevelopmentStorage=true")
            .UseBlobStorageLockManager();

        //act
        using var provider = services.BuildServiceProvider();
        var membership = provider.GetRequiredService<ISingletonMembership>();

        //assert
        membership.HostId.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Should_keep_the_lock_manager_when_membership_is_removed()
    {
        //act
        var services = new ServiceCollection()
            .UseBlobStorageLockManager()
            .RemoveAll<ISingletonMembership>();

        //assert
        services.Count(x => x.ServiceType == typeof(ISingletonLockManager)).Should().Be(1);
        services.Any(x => x.ServiceType == typeof(ISingletonMembership)).Should().BeFalse();
    }
}
