using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Quartz;

namespace KnightBus.Schedule.Tests.Unit;

[TestFixture]
public class JobExecutorTests
{
    [Test]
    public async Task Should_execute_processor()
    {
        //arrange
        var lockHandle = new Mock<ISingletonLockHandle>();
        var lockManager = new Mock<ISingletonLockManager>();
        lockManager
            .Setup(x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    TimeSpan.FromSeconds(60),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(lockHandle.Object);
        var processor = new Mock<IProcessSchedule<DummySchedule>>();
        var di = new Mock<IDependencyInjection>();
        di.Setup(x => x.GetScope()).Returns(di.Object);
        di.Setup(x => x.GetInstances<IProcessSchedule<DummySchedule>>())
            .Returns(new[] { processor.Object });
        var executor = new JobExecutor<DummySchedule, IProcessSchedule<DummySchedule>>(
            Mock.Of<ILogger>(),
            lockManager.Object,
            di.Object
        );
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
        lockManager
            .Setup(x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    TimeSpan.FromSeconds(60),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws<Exception>();
        var processor = new Mock<IProcessSchedule<DummySchedule>>();
        var di = new Mock<IDependencyInjection>();
        di.Setup(x => x.GetScope()).Returns(di.Object);
        di.Setup(x => x.GetInstances<IProcessSchedule<DummySchedule>>())
            .Returns(new[] { processor.Object });
        var executor = new JobExecutor<DummySchedule, IProcessSchedule<DummySchedule>>(
            Mock.Of<ILogger>(),
            lockManager.Object,
            di.Object
        );
        //act & assert
        await executor.Execute(Mock.Of<IJobExecutionContext>());
    }

    [Test]
    public async Task Should_not_throw_on_processor_exceptions()
    {
        //arrange
        var lockHandle = new Mock<ISingletonLockHandle>();
        var lockManager = new Mock<ISingletonLockManager>();
        lockManager
            .Setup(x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    TimeSpan.FromSeconds(60),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(lockHandle.Object);
        var processor = new Mock<IProcessSchedule<DummySchedule>>();
        processor.Setup(x => x.ProcessAsync(It.IsAny<CancellationToken>())).Throws<Exception>();
        var di = new Mock<IDependencyInjection>();
        di.Setup(x => x.GetScope()).Returns(di.Object);
        di.Setup(x => x.GetInstances<IProcessSchedule<DummySchedule>>())
            .Returns(new[] { processor.Object });
        var executor = new JobExecutor<DummySchedule, IProcessSchedule<DummySchedule>>(
            Mock.Of<ILogger>(),
            lockManager.Object,
            di.Object
        );
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
        lockManager
            .Setup(x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    TimeSpan.FromSeconds(60),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ISingletonLockHandle?)null);
        var processor = new Mock<IProcessSchedule<DummySchedule>>();
        var di = new Mock<IDependencyInjection>();
        di.Setup(x => x.GetScope()).Returns(di.Object);
        di.Setup(x => x.GetInstances<IProcessSchedule<DummySchedule>>())
            .Returns(new[] { processor.Object });
        var executor = new JobExecutor<DummySchedule, IProcessSchedule<DummySchedule>>(
            Mock.Of<ILogger>(),
            lockManager.Object,
            di.Object
        );
        //act
        await executor.Execute(Mock.Of<IJobExecutionContext>());
        //assert
        processor.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Should_execute_own_processor_when_processors_share_schedule()
    {
        //arrange
        var lockHandle = new Mock<ISingletonLockHandle>();
        var lockManager = new Mock<ISingletonLockManager>();
        lockManager
            .Setup(x =>
                x.TryLockAsync(
                    It.IsAny<string>(),
                    TimeSpan.FromSeconds(60),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(lockHandle.Object);
        var services = new ServiceCollection();
        services
            .RegisterSchedule<SharedScheduleProcessorOne, DummySchedule>()
            .RegisterSchedule<SharedScheduleProcessorTwo, DummySchedule>();
        using var provider = services.BuildServiceProvider();
        using var di = new MicrosoftDependencyInjection(provider);
        SharedScheduleProcessorOne.Invocations = 0;
        SharedScheduleProcessorTwo.Invocations = 0;
        var executorOne = new JobExecutor<DummySchedule, SharedScheduleProcessorOne>(
            Mock.Of<ILogger>(),
            lockManager.Object,
            di
        );
        var executorTwo = new JobExecutor<DummySchedule, SharedScheduleProcessorTwo>(
            Mock.Of<ILogger>(),
            lockManager.Object,
            di
        );
        //act
        await executorOne.Execute(Mock.Of<IJobExecutionContext>());
        await executorTwo.Execute(Mock.Of<IJobExecutionContext>());
        //assert
        Assert.That(SharedScheduleProcessorOne.Invocations, Is.EqualTo(1));
        Assert.That(SharedScheduleProcessorTwo.Invocations, Is.EqualTo(1));
    }

    public class DummySchedule : ISchedule
    {
        public string CronExpression { get; } = null!;
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
    }

    public class SharedScheduleProcessorOne : IProcessSchedule<DummySchedule>
    {
        public static int Invocations;

        public Task ProcessAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Invocations);
            return Task.CompletedTask;
        }
    }

    public class SharedScheduleProcessorTwo : IProcessSchedule<DummySchedule>
    {
        public static int Invocations;

        public Task ProcessAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Invocations);
            return Task.CompletedTask;
        }
    }
}
