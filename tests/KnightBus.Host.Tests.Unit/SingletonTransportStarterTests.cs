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
                .ReturnsAsync(null);
            var underlyingReader = new Mock<IStartTransport>();
            underlyingReader.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
            var singletonStarter = new SingletonTransportStarter(underlyingReader.Object, lockManager.Object, Mock.Of<ILog>()) { TimerIntervalMs = 1000 };
            //act
            await singletonStarter.StartAsync();
            await singletonStarter.StartAsync();
            await Task.Delay(1001);
            //assert
            underlyingReader.Verify(x => x.StartAsync(), Times.Once);
        }

        [Test]
        public async Task Should_start_new_queue_reader_when_lock_is_released()
        {
            //arrange
            var lockManager = new Mock<ISingletonLockManager>();
            lockManager.SetupSequence(x => x.TryLockAsync(It.IsAny<string>(), TimeSpan.FromSeconds(60), CancellationToken.None))
                .ReturnsAsync(Mock.Of<ISingletonLockHandle>())
                .ReturnsAsync(null)
                .ReturnsAsync(Mock.Of<ISingletonLockHandle>());
            var underlyingReader = new Mock<IStartTransport>();
            underlyingReader.Setup(x => x.Settings).Returns(new Mock<IProcessingSettings>().Object);
            var singletonStarter = new SingletonTransportStarter(underlyingReader.Object, lockManager.Object, Mock.Of<ILog>()) { TimerIntervalMs = 1000 };
            //act
            await singletonStarter.StartAsync();
            await singletonStarter.StartAsync();
            await Task.Delay(1500);
            //assert
            underlyingReader.Verify(x => x.StartAsync(), Times.Exactly(2));
        }

        [Test]
        public void Should_override_singleton_impacted_settings()
        {
            //arrange
            var lockManager = new Mock<ISingletonLockManager>();
            var underlyingReader = new Mock<IStartTransport>();
            underlyingReader.Setup(x => x.Settings).Returns(new SingletonHorrificSettings());
            //act
            var singletonStarter = new SingletonTransportStarter(underlyingReader.Object, lockManager.Object, Mock.Of<ILog>()) { TimerIntervalMs = 1000 };
            //assert
            singletonStarter.Settings.PrefetchCount.Should().Be(0);
            singletonStarter.Settings.MaxConcurrentCalls.Should().Be(1);
            singletonStarter.Settings.MessageLockTimeout.Should().Be(TimeSpan.MaxValue);
            singletonStarter.Settings.DeadLetterDeliveryLimit.Should().Be(1);
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