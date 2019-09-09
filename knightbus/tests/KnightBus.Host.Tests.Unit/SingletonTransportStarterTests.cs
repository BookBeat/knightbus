using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using KnightBus.Host.Singleton;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit
{
    [TestFixture]
    public class SingletonTransportStarterTests
    {
        [Test]
        public async Task Should_only_start_one_queue_reader()
        {
            //arrange
            var lockManager = new Mock<ISingletonLockManager>();
            lockManager.SetupSequence(x => x.TryLockAsync(It.IsAny<string>(), TimeSpan.FromSeconds(60), CancellationToken.None))
                .ReturnsAsync(Mock.Of<ISingletonLockHandle>())
                .ReturnsAsync((ISingletonLockHandle) null);
            var underlyingReader = new Mock<IChannelReceiver>();
            underlyingReader.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
            var singletonChannelReceiver = new SingletonChannelReceiver(underlyingReader.Object, lockManager.Object, Mock.Of<ILog>()) { TimerIntervalMs = 1000 };
            //act
            await singletonChannelReceiver.StartAsync(CancellationToken.None);
            await singletonChannelReceiver.StartAsync(CancellationToken.None);
            await Task.Delay(1001);
            //assert
            underlyingReader.Verify(x => x.StartAsync(CancellationToken.None), Times.Once);
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
            var underlyingReader = new Mock<IChannelReceiver>();
            underlyingReader.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
            var singletonChannelReceiver = new SingletonChannelReceiver(underlyingReader.Object, lockManager.Object, Mock.Of<ILog>()) { TimerIntervalMs = 1000 };
            //act
            await singletonChannelReceiver.StartAsync(CancellationToken.None);
            await singletonChannelReceiver.StartAsync(CancellationToken.None);
            await Task.Delay(1500);
            //assert
            underlyingReader.Verify(x => x.StartAsync(CancellationToken.None), Times.Exactly(2));
        }

        [Test]
        public void Should_override_singleton_impacted_settings()
        {
            //arrange
            var lockManager = new Mock<ISingletonLockManager>();
            var underlyingReader = new Mock<IChannelReceiver>();
            underlyingReader.Setup(x => x.Settings).Returns(new SingletonHorrificSettings());
            //act
            var starter = new SingletonChannelReceiver(underlyingReader.Object, lockManager.Object, Mock.Of<ILog>()) { TimerIntervalMs = 1000 };
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