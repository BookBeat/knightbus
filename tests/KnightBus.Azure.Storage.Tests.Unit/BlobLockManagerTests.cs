using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Azure.Storage.Singleton;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Unit;

[TestFixture]
public class BlobLockManagerTests
{
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
