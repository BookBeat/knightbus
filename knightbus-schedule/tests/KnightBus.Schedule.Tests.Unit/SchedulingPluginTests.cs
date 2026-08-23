using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Schedule.Tests.Unit;

[TestFixture]
public class SchedulingPluginTests
{
    [Test]
    public async Task Should_allow_stop_before_start()
    {
        //arrange
        var plugin = new SchedulingPlugin(
            Mock.Of<IHostConfiguration>(),
            Mock.Of<ILogger<SchedulingPlugin>>()
        );

        //act & assert
        Assert.DoesNotThrowAsync(() => plugin.StopAsync(CancellationToken.None));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Should_shut_down_the_scheduler_on_stop()
    {
        //arrange
        var dependencyInjection = new Mock<IDependencyInjection>();
        dependencyInjection
            .Setup(x => x.GetInstance<ISingletonLockManager>())
            .Returns(Mock.Of<ISingletonLockManager>());
        dependencyInjection
            .Setup(x => x.GetOpenGenericRegistrations(typeof(IProcessSchedule<>)))
            .Returns(Array.Empty<Type>());
        var configuration = new Mock<IHostConfiguration>();
        configuration.Setup(x => x.DependencyInjection).Returns(dependencyInjection.Object);
        configuration.Setup(x => x.Log).Returns(Mock.Of<ILogger>());
        var plugin = new SchedulingPlugin(
            configuration.Object,
            Mock.Of<ILogger<SchedulingPlugin>>()
        );
        await plugin.StartAsync(CancellationToken.None);

        //act & assert: stopping must complete promptly when no schedules are running
        await plugin
            .StopAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(10))
            .ConfigureAwait(false);
    }
}
