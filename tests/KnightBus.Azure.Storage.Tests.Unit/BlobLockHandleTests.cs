using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using KnightBus.Azure.Storage.Singleton;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Azure.Storage.Tests.Unit;

[TestFixture]
public class BlobLockHandleTests
{
    private static BlobLockHandle CreateHandle(TimeSpan leasePeriod, Exception renewalFailure)
    {
        var lease = new Mock<BlobLeaseClient>();
        lease
            .Setup(x => x.RenewAsync(It.IsAny<RequestConditions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(renewalFailure);
        return new BlobLockHandle("lease", "lock", lease.Object, leasePeriod);
    }

    [Test]
    public async Task Should_report_transient_failure_while_the_lease_is_still_valid()
    {
        //arrange
        var handle = CreateHandle(
            TimeSpan.FromMinutes(1),
            new RequestFailedException(503, "server busy")
        );
        //act
        var renewed = await handle.RenewAsync(Mock.Of<ILogger>(), CancellationToken.None);
        //assert: the scope should retry quickly, a single 5xx must not drop the lock
        renewed.Should().BeFalse();
    }

    [Test]
    public async Task Should_give_up_when_server_errors_outlast_the_lease_period()
    {
        //arrange
        var handle = CreateHandle(
            TimeSpan.FromMilliseconds(200),
            new RequestFailedException(503, "server busy")
        );
        (await handle.RenewAsync(Mock.Of<ILogger>(), CancellationToken.None)).Should().BeFalse();
        //act: the lease has now run out on the server
        await Task.Delay(300);
        var renew = () => handle.RenewAsync(Mock.Of<ILogger>(), CancellationToken.None);
        //assert: the holder must stop instead of processing without a lock
        await renew.Should().ThrowAsync<RequestFailedException>();
    }

    [Test]
    public async Task Should_throw_when_the_lease_is_lost()
    {
        //arrange
        var handle = CreateHandle(
            TimeSpan.FromMinutes(1),
            new RequestFailedException(409, "lease lost")
        );
        //act
        var renew = () => handle.RenewAsync(Mock.Of<ILogger>(), CancellationToken.None);
        //assert
        await renew.Should().ThrowAsync<RequestFailedException>();
    }
}
