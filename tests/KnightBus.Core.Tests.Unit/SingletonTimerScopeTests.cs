using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class SingletonTimerScopeTests
{
    [Test]
    public async Task Should_release_lock_even_when_cancellation_source_is_disposed_immediately()
    {
        //The owner can cancel and dispose the CancellationTokenSource right after the scope
        //is created, like SingletonChannelReceiver does on shutdown. Run many iterations
        //since the failure is a race between the scope's loop starting and the disposal.
        for (var i = 0; i < 50; i++)
        {
            //arrange
            var released = new TaskCompletionSource();
            var handle = new Mock<ISingletonLockHandle>();
            handle
                .Setup(x => x.ReleaseAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    released.TrySetResult();
                    return Task.CompletedTask;
                });
            var cts = new CancellationTokenSource();
            _ = new SingletonTimerScope(
                Mock.Of<ILogger>(),
                handle.Object,
                true,
                TimeSpan.FromSeconds(19),
                cts
            );

            //act
            cts.Cancel();
            cts.Dispose();

            //assert
            await released
                .Task.WaitAsync(TimeSpan.FromSeconds(5))
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            released
                .Task.IsCompletedSuccessfully.Should()
                .BeTrue(
                    "the lock must be released even when the cancellation source is cancelled "
                        + $"and disposed right after the scope is created (iteration {i})"
                );
        }
    }

    [Test]
    public async Task Should_not_return_from_dispose_while_release_is_in_flight()
    {
        //arrange
        var releaseEntered = new TaskCompletionSource();
        var releaseGate = new TaskCompletionSource();
        var handle = new Mock<ISingletonLockHandle>();
        handle
            .Setup(x => x.RenewAsync(It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        handle
            .Setup(x => x.ReleaseAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                releaseEntered.TrySetResult();
                return releaseGate.Task;
            });
        using var cts = new CancellationTokenSource();
        var scope = new SingletonTimerScope(
            Mock.Of<ILogger>(),
            handle.Object,
            true,
            TimeSpan.FromSeconds(19),
            cts
        );

        //act: cancelling starts the release from the scope's own loop
        cts.Cancel();
        await releaseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disposeTask = Task.Run(() => scope.Dispose());
        var winner = await Task.WhenAny(disposeTask, Task.Delay(300));

        //assert
        winner
            .Should()
            .NotBe(
                disposeTask,
                "Dispose must not return while the lock release is still in flight"
            );
        releaseGate.TrySetResult();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
