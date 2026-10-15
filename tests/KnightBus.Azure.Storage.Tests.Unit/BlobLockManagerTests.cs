using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Azure.Storage.Singleton;
using Moq;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Unit;

[TestFixture]
public class BlobLockManagerTests
{
    [TestCase("locks", "locks/lock")]
    [TestCase("locks/", "locks/lock")]
    [TestCase("locks//", "locks//lock")]
    public void Should_name_lock_blobs_the_way_earlier_versions_did(
        string directory,
        string expected
    )
    {
        //A different name during a rolling upgrade would let old and new hosts both take the lock
        var scheme = new Mock<IBlobLockScheme>();
        scheme.Setup(x => x.Directory).Returns(directory);

        BlobLockManager.BlobName(scheme.Object, "lock").Should().Be(expected);
    }

    [TestCase(90)]
    [TestCase(10)]
    public async Task Should_reject_a_lease_duration_the_blob_service_does_not_accept(int seconds)
    {
        //arrange
        var lockManager = new BlobLockManager("UseDevelopmentStorage=true");
        await lockManager.InitializeAsync();

        //act
        var take = () =>
            lockManager.TryLockAsync("lock", TimeSpan.FromSeconds(seconds), CancellationToken.None);

        //assert
        await take.Should()
            .ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*15 and 60 seconds*");
    }
}
