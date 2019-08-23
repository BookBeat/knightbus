using System;
using System.Threading;
using System.Threading.Tasks;
using BB.Common.BlobStorage.Singleton;
using Moq;
using NUnit.Framework;
using Serilog;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace BB.Common.KnightWatch.Tests.Unit
{
    [TestFixture]
    public class TriggerTests
    {
        readonly string _connectionString = "DefaultEndpointsProtocol=https;AccountName=devbbwebjobs;AccountKey=bXKSayl97bfpwieFXaP8W0PmuDSiTdaR4ULSSYH4RKeT/i7mfjplpo/S2UAI+WcqCgTTRVTuwq4vwo3S3LJU4w==;EndpointSuffix=core.windows.net";

        [Test]
        public void Should_throw_if_no_SingletonLockManager_is_registered()
        {
            //arrange
            var trigger = new Mock<IProcessTrigger<TestTriggerSettings>>();
            var log = new Mock<ILogger>();
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            container.RegisterSingleton(log.Object);
            container.RegisterSingleton(trigger.Object);
            container.Verify();
            var scheduler = new KnightWatchHost(container);

            //act and assert
            Assert.Throws<ActivationException>(() => scheduler.Start());
        }

        [Test]
        public async Task Should_execute_trigger()
        {
            //arrange
            var trigger = new Mock<IProcessTrigger<TestTriggerSettings>>();
            var log = new Mock<ILogger>();
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            container.RegisterSingleton(log.Object);
            container.RegisterSingleton(trigger.Object);
            container.RegisterSingleton<ISingletonLockManager>(new SingletonLockManager(_connectionString));
            container.Verify();
            var scheduler = new KnightWatchHost(container);
            //act
            scheduler.Start();
            await Task.Delay(5000);
            //assert
            trigger.Verify(x => x.ProcessAsync(), Times.AtLeast(1));
        }

        [Test]
        public async Task Should_renew_lease()
        {
            //arrange
            var trigger = new Mock<IProcessTrigger<TestTriggerSettings>>();
            trigger.Setup(x => x.ProcessAsync()).Callback(() => Task.Delay(70000).GetAwaiter().GetResult());
            var log = new Mock<ILogger>();
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            container.RegisterSingleton(log.Object);
            container.RegisterSingleton(trigger.Object);
            container.RegisterSingleton<ISingletonLockManager>(new SingletonLockManager(_connectionString));
            container.Verify();
            var scheduler = new KnightWatchHost(container);
            //act
            scheduler.Start();
            await Task.Delay(70000);
            //assert
            trigger.Verify(x => x.ProcessAsync(), Times.Once);
        }

        [Test]
        public async Task Should_not_execute_trigger_if_locked()
        {
            //arrange
            var trigger = new Mock<IProcessTrigger<TestTriggerSettings>>();
            var singletonLock = new Mock<ISingletonLockManager>();
            singletonLock.Setup(x => x.TryLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), CancellationToken.None)).ReturnsAsync((SingletonLockHandle) null);
            var log = new Mock<ILogger>();
            var container = new Container();
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            container.RegisterSingleton(log.Object);
            container.RegisterSingleton(trigger.Object);
            container.RegisterSingleton(singletonLock.Object);
            container.Verify();
            var scheduler = new KnightWatchHost(container);
            //act
            scheduler.Start();
            await Task.Delay(1000);
            //assert
            trigger.Verify(x => x.ProcessAsync(), Times.Never);
        }

        public class TestTriggerSettings : ITriggerSettings
        {
            public string CronExpression => "* * * * * ?"; //every second
        }

        public class ReallyLongRunning : IProcessTrigger<TestTriggerSettings>
        {

            public Task ProcessAsync()
            {
                return Task.Delay(TimeSpan.FromSeconds(70));
            }
        }
    }
}