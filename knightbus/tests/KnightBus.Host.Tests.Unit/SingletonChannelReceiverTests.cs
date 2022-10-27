using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using KnightBus.Host.Singleton;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit
{
    [TestFixture]
    public class SingletonChannelReceiverTests
    {
        [Test]
        public async Task Should_only_start_one_queue_reader()
        {
            //arrange
            var lockManager = new Mock<ISingletonLockManager>();
            lockManager.SetupSequence(x => x.TryLockAsync(It.IsAny<string>(), TimeSpan.FromSeconds(60), CancellationToken.None))
                .ReturnsAsync(Mock.Of<ISingletonLockHandle>())
                .ReturnsAsync((ISingletonLockHandle) null);
            var underlyingReceiver = new Mock<IChannelReceiver>();
            underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
            var singletonChannelReceiver = new SingletonChannelReceiver(underlyingReceiver.Object, lockManager.Object, Mock.Of<ILogger>()) { TimerInterval = TimeSpan.FromSeconds(1) };
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
            lockManager.SetupSequence(x => x.TryLockAsync(It.IsAny<string>(), TimeSpan.FromSeconds(60), CancellationToken.None))
                .ReturnsAsync(Mock.Of<ISingletonLockHandle>())
                .ReturnsAsync((ISingletonLockHandle) null)
                .ReturnsAsync(Mock.Of<ISingletonLockHandle>());
            var underlyingReceiver = new Mock<IChannelReceiver>();
            underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
            var singletonChannelReceiver = new SingletonChannelReceiver(underlyingReceiver.Object, lockManager.Object, Mock.Of<ILogger>()) { TimerInterval = TimeSpan.FromSeconds(1) };
            //act
            await singletonChannelReceiver.StartAsync(CancellationToken.None);
            await singletonChannelReceiver.StartAsync(CancellationToken.None);
            await Task.Delay(3000);
            //assert
            underlyingReceiver.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public async Task Should_restart_queue_reader_when_lock_is_lost()
        {
            //arrange
            var handle = new Mock<ISingletonLockHandle>();
            var lockManager = new Mock<ISingletonLockManager>();
            lockManager.Setup(x => x.TryLockAsync(It.IsAny<string>(), TimeSpan.FromSeconds(60), CancellationToken.None))
                .ReturnsAsync(handle.Object);

            handle.SetupSequence(x => x.RenewAsync(It.IsAny<ILogger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .Throws(new Exception())
                .ReturnsAsync(true);

            var underlyingReceiver = new Mock<IChannelReceiver>();
            underlyingReceiver.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
            var singletonChannelReceiver = new SingletonChannelReceiver(underlyingReceiver.Object, lockManager.Object, Mock.Of<ILogger>())
            {
                TimerInterval = TimeSpan.FromSeconds(1),
                LockRefreshInterval = TimeSpan.FromSeconds(1)
            };
            //act
            await singletonChannelReceiver.StartAsync(CancellationToken.None);
            await Task.Delay(2100); 
            //assert
            underlyingReceiver.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public void Should_override_singleton_impacted_settings()
        {
            //arrange
            var lockManager = new Mock<ISingletonLockManager>();
            var underlyingReceiver = new Mock<IChannelReceiver>();
            underlyingReceiver.Setup(x => x.Settings).Returns(new SingletonHorrificSettings());
            //act
            var starter = new SingletonChannelReceiver(underlyingReceiver.Object, lockManager.Object, Mock.Of<ILogger>()) { TimerInterval = TimeSpan.FromSeconds(1) };
            //assert
            starter.Settings.PrefetchCount.Should().Be(0);
            starter.Settings.MaxConcurrentCalls.Should().Be(1);
            starter.Settings.MessageLockTimeout.Should().Be(TimeSpan.MaxValue);
            starter.Settings.DeadLetterDeliveryLimit.Should().Be(1);
        }

        public class SingletonHorrificSettings : IProcessingSettings
        {
            public int MaxConcurrentCalls => 200;
            public int PrefetchCount => 500;
            public TimeSpan MessageLockTimeout => TimeSpan.MaxValue;
            public int DeadLetterDeliveryLimit => 1;
        }
    }
}
