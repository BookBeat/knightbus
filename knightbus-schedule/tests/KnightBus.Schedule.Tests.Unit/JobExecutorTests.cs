using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Quartz;

namespace KnightBus.Schedule.Tests.Unit
{
    [TestFixture]
    public class JobExecutorTests
    {
        [Test]
        public async Task Should_execute_processor()
        {
            //arrange
            var lockHandle = new Mock<ISingletonLockHandle>();
            var lockManager = new Mock<ISingletonLockManager>();
            lockManager.Setup(x => x.TryLockAsync(typeof(DummySchedule).FullName, TimeSpan.FromSeconds(60), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lockHandle.Object);
            var processor = new Mock<IProcessSchedule<DummySchedule>>();
            var di = new Mock<IDependencyInjection>();
            di.Setup(x => x.GetScope()).Returns(di.Object);
            di.Setup(x => x.GetInstance<IProcessSchedule<DummySchedule>>()).Returns(processor.Object);
            var executor = new JobExecutor<DummySchedule>(Mock.Of<ILogger>(), lockManager.Object, di.Object);
            //act
            await executor.Execute(Mock.Of<IJobExecutionContext>());
            //assert
            processor.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Should_not_throw_on_lock_exceptions()
        {
            //arrange
            var lockManager = new Mock<ISingletonLockManager>();
            lockManager.Setup(x => x.TryLockAsync(typeof(DummySchedule).FullName, TimeSpan.FromSeconds(60), It.IsAny<CancellationToken>()))
                .Throws<Exception>();
            var processor = new Mock<IProcessSchedule<DummySchedule>>();
            var di = new Mock<IDependencyInjection>();
            di.Setup(x => x.GetScope()).Returns(di.Object);
            di.Setup(x => x.GetInstance<IProcessSchedule<DummySchedule>>()).Returns(processor.Object);
            var executor = new JobExecutor<DummySchedule>(Mock.Of<ILogger>(), lockManager.Object, di.Object);
            //act & assert
            await executor.Execute(Mock.Of<IJobExecutionContext>());
        }

        [Test]
        public async Task Should_not_throw_on_processor_exceptions()
        {
            //arrange
            var lockHandle = new Mock<ISingletonLockHandle>();
            var lockManager = new Mock<ISingletonLockManager>();
            lockManager.Setup(x => x.TryLockAsync(typeof(DummySchedule).FullName, TimeSpan.FromSeconds(60), It.IsAny<CancellationToken>()))
                .ReturnsAsync(lockHandle.Object);
            var processor = new Mock<IProcessSchedule<DummySchedule>>();
            processor.Setup(x => x.ProcessAsync(It.IsAny<CancellationToken>())).Throws<Exception>();
            var di = new Mock<IDependencyInjection>();
            di.Setup(x => x.GetScope()).Returns(di.Object);
            di.Setup(x => x.GetInstance<IProcessSchedule<DummySchedule>>()).Returns(processor.Object);
            var executor = new JobExecutor<DummySchedule>(Mock.Of<ILogger>(), lockManager.Object, di.Object);
            //act
            await executor.Execute(Mock.Of<IJobExecutionContext>());
            //assert
            processor.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Should_not_execute_processor_when_no_lock()
        {
            //arrange
            var lockManager = new Mock<ISingletonLockManager>();
            lockManager.Setup(x => x.TryLockAsync(typeof(DummySchedule).FullName, TimeSpan.FromSeconds(60), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ISingletonLockHandle)null);
            var processor = new Mock<IProcessSchedule<DummySchedule>>();
            var di = new Mock<IDependencyInjection>();
            di.Setup(x => x.GetScope()).Returns(di.Object);
            di.Setup(x => x.GetInstance<IProcessSchedule<DummySchedule>>()).Returns(processor.Object);
            var executor = new JobExecutor<DummySchedule>(Mock.Of<ILogger>(), lockManager.Object, di.Object);
            //act
            await executor.Execute(Mock.Of<IJobExecutionContext>());
            //assert
            processor.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        public class DummySchedule : ISchedule
        {
            public string CronExpression { get; }
            public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
        }
    }
}
