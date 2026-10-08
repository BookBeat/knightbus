using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Azure;
using Azure.Storage.Blobs;
using KnightBus.Azure.Storage.Singleton;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Integration;

[TestFixture]
public class BlobLockManagerTests
{
    ///Azurite docker connection
    private string _connection = StorageSetup.ConnectionString;

    [Test]
    [Parallelizable]
    public async Task Should_create_new_lock()
    {
        //arrange
        var lockManager = new BlobLockManager(
            StorageSetup.ConnectionString,
            new DefaultBlobLockScheme()
        );
        await lockManager.InitializeAsync();
        var lockId = Guid.NewGuid().ToString();
        //act
        var handle = await lockManager.TryLockAsync(
            lockId,
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //assert
        handle.Should().NotBeNull("Lock should be acquired");
        handle.LeaseId.Should().NotBeNullOrWhiteSpace();
        handle.LockId.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    [Parallelizable]
    public async Task Should_record_the_holding_host_in_blob_metadata()
    {
        //arrange
        var scheme = new DefaultBlobLockScheme();
        var lockManager = new BlobLockManager(StorageSetup.ConnectionString, scheme);
        await lockManager.InitializeAsync();
        var lockId = Guid.NewGuid().ToString();
        //act
        var handle = await lockManager.TryLockAsync(
            lockId,
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //assert
        handle.Should().NotBeNull("Lock should be acquired");
        var blob = new BlobContainerClient(
            StorageSetup.ConnectionString,
            scheme.ContainerName
        ).GetBlobClient(Path.Combine(scheme.Directory, lockId));
        var metadata = (await blob.GetPropertiesAsync()).Value.Metadata;
        metadata["FunctionInstance"].Should().Be(lockId);
        metadata["HostName"].Should().Be(Environment.MachineName);
        DateTimeOffset
            .Parse(metadata["AcquiredAtUtc"])
            .Should()
            .BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Test]
    [Parallelizable]
    public async Task Should_not_get_lease_when_already_locked()
    {
        //arrange
        var lockManager = new BlobLockManager(
            StorageSetup.ConnectionString,
            new DefaultBlobLockScheme()
        );
        await lockManager.InitializeAsync();
        var lockId = Guid.NewGuid().ToString();
        await lockManager.TryLockAsync(lockId, TimeSpan.FromMinutes(1), CancellationToken.None);
        //act
        var secondHandle = await lockManager.TryLockAsync(
            lockId,
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //assert
        secondHandle.Should().BeNull("Already locked");
    }

    [Test]
    [Parallelizable]
    public async Task Should_release_lease()
    {
        //arrange
        var lockManager = new BlobLockManager(
            StorageSetup.ConnectionString,
            new DefaultBlobLockScheme()
        );
        await lockManager.InitializeAsync();
        var lockId = Guid.NewGuid().ToString();
        var handle = await lockManager.TryLockAsync(
            lockId,
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //act
        await handle!.ReleaseAsync(CancellationToken.None);
        var secondHandle = await lockManager.TryLockAsync(
            lockId,
            TimeSpan.FromMinutes(1),
            CancellationToken.None
        );
        //assert
        secondHandle.Should().NotBeNull();
    }

    [Test]
    [Parallelizable]
    public async Task Should_renew_lease()
    {
        //arrange
        var lockManager = new BlobLockManager(
            StorageSetup.ConnectionString,
            new DefaultBlobLockScheme()
        );
        await lockManager.InitializeAsync();
        var lockId = Guid.NewGuid().ToString();
        var handle = await lockManager.TryLockAsync(
            lockId,
            TimeSpan.FromSeconds(15),
            CancellationToken.None
        );
        //act
        await Task.Delay(TimeSpan.FromSeconds(10));
        var renewed = await handle!.RenewAsync(Mock.Of<ILogger>(), CancellationToken.None);
        //assert
        renewed.Should().BeTrue();
    }

    [Test]
    [Parallelizable]
    public async Task Should_not_renew_expired_lease()
    {
        //arrange
        var lockManager = new BlobLockManager(
            StorageSetup.ConnectionString,
            new DefaultBlobLockScheme()
        );
        await lockManager.InitializeAsync();
        var lockId = Guid.NewGuid().ToString();
        var handle = await lockManager.TryLockAsync(
            lockId,
            TimeSpan.FromSeconds(15),
            CancellationToken.None
        );
        //act
        await Task.Delay(TimeSpan.FromSeconds(16));
        //steal lock
        await lockManager.TryLockAsync(lockId, TimeSpan.FromSeconds(15), CancellationToken.None);
        await handle!
            .Awaiting(x => x.RenewAsync(Mock.Of<ILogger>(), CancellationToken.None))
            .Should()
            .ThrowAsync<RequestFailedException>();
    }
}
