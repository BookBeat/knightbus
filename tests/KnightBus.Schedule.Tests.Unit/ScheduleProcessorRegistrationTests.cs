using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace KnightBus.Schedule.Tests.Unit;

public class ScheduleProcessorRegistrationTests
{
    [Test]
    public void Enumerating_processors_for_shared_schedule_returns_all_registrations()
    {
        // Arrange
        var services = new ServiceCollection();
        services
            .RegisterSchedule<SharedScheduleProcessorOne, SharedSchedule>()
            .RegisterSchedule<SharedScheduleProcessorTwo, SharedSchedule>()
            .RegisterSchedule<IndependentScheduleProcessor, IndependentSchedule>();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
        using var scope = provider.CreateScope();

        // Act
        var processors = scope
            .ServiceProvider.GetServices<IProcessSchedule<SharedSchedule>>()
            .Select(p => p.GetType())
            .ToList();

        // Assert
        processors
            .Should()
            .BeEquivalentTo([
                typeof(SharedScheduleProcessorOne),
                typeof(SharedScheduleProcessorTwo),
            ]);
    }

    [Test]
    public void Open_generic_schedule_lookup_returns_each_concrete_processor()
    {
        // Arrange
        var services = new ServiceCollection();
        services
            .RegisterSchedule<SharedScheduleProcessorOne, SharedSchedule>()
            .RegisterSchedule<SharedScheduleProcessorTwo, SharedSchedule>()
            .RegisterSchedule<IndependentScheduleProcessor, IndependentSchedule>();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
        using var di = new MicrosoftDependencyInjection(provider);

        // Act
        var registrations = di.GetOpenGenericRegistrations(typeof(IProcessSchedule<>)).ToList();

        // Assert
        registrations
            .Should()
            .BeEquivalentTo([
                typeof(SharedScheduleProcessorOne),
                typeof(SharedScheduleProcessorTwo),
                typeof(IndependentScheduleProcessor),
            ]);
    }

    private record SharedSchedule : ISchedule
    {
        public string CronExpression => "0 */5 * ? * *";
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
    }

    private record IndependentSchedule : ISchedule
    {
        public string CronExpression => "0 */10 * ? * *";
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
    }

    private class SharedScheduleProcessorOne : IProcessSchedule<SharedSchedule>
    {
        public Task ProcessAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private class SharedScheduleProcessorTwo : IProcessSchedule<SharedSchedule>
    {
        public Task ProcessAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private class IndependentScheduleProcessor : IProcessSchedule<IndependentSchedule>
    {
        public Task ProcessAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
