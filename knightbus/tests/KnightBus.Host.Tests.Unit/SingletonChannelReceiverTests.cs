using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using KnightBus.Host.Singleton;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

[TestFixture]
public class SingletonChannelReceiverTests
{
    [Test]
    public async Task Should_only_start_one_queue_reader()
    {
        //arrange
        var lockManager = new Mock<ISingletonLockManager>();
        lockManager
            .SetupSequence(x =>
                x.TryLockAsync(It.IsAny<string>(), TimeSpan.FromSeconds(60), CancellationToken.None)
            )
            .ReturnsAsync(Mock.Of<ISingletonLockHandle>())
            .ReturnsAsync((ISingletonLockHandle)null);
        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
        var singletonChannelReceiver = new SingletonChannelReceiver(
            underlyingReceiver.Object,
            lockManager.Object,
            Mock.Of<ILogger>()
        )
        {
            TimerInterval = TimeSpan.FromSeconds(1),
        };
        //act
        await singletonChannelReceiver.StartAsync(CancellationToken.None);
        await singletonChannelReceiver.StartAsync(CancellationToken.None);
        await Task.Delay(1001);
        //assert
        underlyingReceiver.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Should_start_new_queue_reader_when_lock_is_released()
    {
        //arrange
        var lockManager = new Mock<ISingletonLockManager>();
        lockManager
            .SetupSequence(x =>
                x.TryLockAsync(It.IsAny<string>(), TimeSpan.FromSeconds(60), CancellationToken.None)
            )
            .ReturnsAsync(Mock.Of<ISingletonLockHandle>())
            .ReturnsAsync((ISingletonLockHandle)null)
            .ReturnsAsync(Mock.Of<ISingletonLockHandle>());
        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
        var singletonChannelReceiver = new SingletonChannelReceiver(
            underlyingReceiver.Object,
            lockManager.Object,
            Mock.Of<ILogger>()
        )
        {
            TimerInterval = TimeSpan.FromSeconds(1),
        };
        //act
        await singletonChannelReceiver.StartAsync(CancellationToken.None);
        await singletonChannelReceiver.StartAsync(CancellationToken.None);
        await Task.Delay(3000);
        //assert
        underlyingReceiver.Verify(
            x => x.StartAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Test]
    public async Task Should_restart_queue_reader_when_lock_is_lost()
    {
        //arrange
        var handle = new Mock<ISingletonLockHandle>();
        var lockManager = new Mock<ISingletonLockManager>();
        lockManager
            .Setup(x =>
                x.TryLockAsync(It.IsAny<string>(), TimeSpan.FromSeconds(60), CancellationToken.None)
            )
            .ReturnsAsync(handle.Object);

        handle
            .SetupSequence(x => x.RenewAsync(It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Throws(new Exception())
            .ReturnsAsync(true);

        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
        var singletonChannelReceiver = new SingletonChannelReceiver(
            underlyingReceiver.Object,
            lockManager.Object,
            Mock.Of<ILogger>()
        )
        {
            TimerInterval = TimeSpan.FromSeconds(1),
            LockRefreshInterval = TimeSpan.FromSeconds(1),
        };
        //act
        await singletonChannelReceiver.StartAsync(CancellationToken.None);
        await Task.Delay(2100);
        //assert
        underlyingReceiver.Verify(
            x => x.StartAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Test]
    public async Task Should_release_lock_when_shutting_down()
    {
        //arrange
        var handle = new Mock<ISingletonLockHandle>();
        handle
            .Setup(x => x.RenewAsync(It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var lockManager = new Mock<ISingletonLockManager>();
        lockManager
            .Setup(x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    TimeSpan.FromSeconds(60),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(handle.Object);
        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
        var singletonChannelReceiver = new SingletonChannelReceiver(
            underlyingReceiver.Object,
            lockManager.Object,
            Mock.Of<ILogger>()
        );
        using var cts = new CancellationTokenSource();
        await singletonChannelReceiver.StartAsync(cts.Token);

        //act
        cts.Cancel();

        //assert
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (
            !handle.Invocations.Any(i => i.Method.Name == nameof(ISingletonLockHandle.ReleaseAsync))
            && DateTime.UtcNow < deadline
        )
        {
            await Task.Delay(10, CancellationToken.None);
        }

        handle.Verify(
            x => x.ReleaseAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "the singleton lock must be released on shutdown so another instance can take over immediately instead of waiting for the lease to expire"
        );
    }

    [Test]
    public async Task Should_hold_lock_until_teardown_when_teardown_token_is_provided()
    {
        //arrange
        var handle = new Mock<ISingletonLockHandle>();
        handle
            .Setup(x => x.RenewAsync(It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var lockManager = new Mock<ISingletonLockManager>();
        lockManager
            .Setup(x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(handle.Object);
        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
        CancellationToken receiverToken = default;
        underlyingReceiver
            .Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Callback<CancellationToken>(t => receiverToken = t)
            .Returns(Task.CompletedTask);
        using var shutdown = new CancellationTokenSource();
        using var teardown = new CancellationTokenSource();
        var singletonChannelReceiver = new SingletonChannelReceiver(
            underlyingReceiver.Object,
            lockManager.Object,
            Mock.Of<ILogger>(),
            teardownToken: teardown.Token
        );
        await singletonChannelReceiver.StartAsync(shutdown.Token);

        //act: phase one stops the wrapped receiver but must keep the lock
        shutdown.Cancel();
        await Task.Delay(700);

        //assert
        receiverToken
            .IsCancellationRequested.Should()
            .BeTrue("the wrapped receiver must stop on the shutdown token");
        handle.Verify(
            x => x.ReleaseAsync(It.IsAny<CancellationToken>()),
            Times.Never,
            "the lock must be held through the drain so no other instance starts processing"
        );

        //act: phase two releases the lock
        teardown.Cancel();
        await singletonChannelReceiver.TeardownCompletion.WaitAsync(TimeSpan.FromSeconds(5));

        //assert
        handle.Verify(x => x.ReleaseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Should_override_singleton_impacted_settings()
    {
        //arrange
        var lockManager = new Mock<ISingletonLockManager>();
        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver.Setup(x => x.Settings).Returns(new SingletonHorrificSettings());
        //act
        var starter = new SingletonChannelReceiver(
            underlyingReceiver.Object,
            lockManager.Object,
            Mock.Of<ILogger>()
        )
        {
            TimerInterval = TimeSpan.FromSeconds(1),
        };
        //assert
        starter.Settings.PrefetchCount.Should().Be(0);
        starter.Settings.MaxConcurrentCalls.Should().Be(1);
        starter.Settings.MessageLockTimeout.Should().Be(TimeSpan.MaxValue);
        starter.Settings.DeadLetterDeliveryLimit.Should().Be(1);
    }

    [Test]
    public void Should_use_channel_receiver_type_as_lock_id_when_no_lock_id_provided()
    {
        //arrange
        var lockManager = new Mock<ISingletonLockManager>();
        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);

        //act
        var starter = new SingletonChannelReceiver(
            underlyingReceiver.Object,
            lockManager.Object,
            Mock.Of<ILogger>()
        );

        //assert
        starter.LockId.Should().Be(underlyingReceiver.Object.GetType().FullName);
    }

    [Test]
    public void Should_use_provided_lock_id_when_specified()
    {
        //arrange
        var lockManager = new Mock<ISingletonLockManager>();
        var underlyingReceiver = new Mock<IChannelReceiver>();
        underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
        var customLockId = "CustomChannelReceiver:MySubscription";

        //act
        var starter = new SingletonChannelReceiver(
            underlyingReceiver.Object,
            lockManager.Object,
            Mock.Of<ILogger>(),
            customLockId
        );

        //assert
        starter.LockId.Should().Be(customLockId);
    }

    [Test]
    public void Should_allow_different_subscriptions_to_have_different_lock_ids()
    {
        //arrange
        var lockManager = new Mock<ISingletonLockManager>();
        var underlyingReceiver1 = new Mock<IChannelReceiver>();
        var underlyingReceiver2 = new Mock<IChannelReceiver>();
        underlyingReceiver1.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
        underlyingReceiver2.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);

        var baseLockId = "OrderCreatedEventReceiver";
        var subscription1LockId = $"{baseLockId}:EmailNotification";
        var subscription2LockId = $"{baseLockId}:InventoryUpdate";

        //act
        var starter1 = new SingletonChannelReceiver(
            underlyingReceiver1.Object,
            lockManager.Object,
            Mock.Of<ILogger>(),
            subscription1LockId
        );
        var starter2 = new SingletonChannelReceiver(
            underlyingReceiver2.Object,
            lockManager.Object,
            Mock.Of<ILogger>(),
            subscription2LockId
        );

        //assert
        starter1.LockId.Should().NotBe(starter2.LockId);
        starter1.LockId.Should().Contain("EmailNotification");
        starter2.LockId.Should().Contain("InventoryUpdate");
    }

    public class SingletonHorrificSettings : IProcessingSettings
    {
        public int MaxConcurrentCalls => 200;
        public int PrefetchCount => 500;
        public TimeSpan MessageLockTimeout => TimeSpan.MaxValue;
        public int DeadLetterDeliveryLimit => 1;
    }
}
