using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.DependencyInjection;
using KnightBus.Schedule;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
    public class TestSchedule : ISchedule
    {
        public string CronExpression { get; }
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
    }

    public class TestScheduleProcessor : IProcessSchedule<TestSchedule>
    {
        public Task ProcessAsync(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }

    public class MicrosoftDependencyInjectionTests
    {
        private MicrosoftDependencyInjection DependencyInjection { get; set; }

        [SetUp]
        public void Setup()
        {
            var container = new ServiceCollection();
            container.AddScoped<ITestService, TestService>();

            DependencyInjection = new MicrosoftDependencyInjection(container.BuildServiceProvider());
        }

        [Test]
        public void DependencyInjection_can_register_open_generic()
        {
            //arrange
            var container = new ServiceCollection();
            container.RegisterSchedules(typeof(TestSchedule).Assembly);
            var di = new MicrosoftDependencyInjection(container.BuildServiceProvider());

            //act
            var resolved = di.GetScope().GetInstance<IProcessSchedule<TestSchedule>>();
            //assert
            resolved.Should().NotBeNull();
        }

        [Test]
        public void DependencyInjection_resolves_services_in_correct_scope()
        {
            // arrange
            ITestService outerScopeService;
            ITestService innerScopeService;
            ITestService secondInnerScopeService;
            ITestService secondOuterScopeService;

            // act
            using (var outerScope = DependencyInjection.GetScope())
            {
                outerScopeService = outerScope.GetInstance<ITestService>();

                using (var innerScope = outerScope.GetScope())
                {
                    innerScopeService = innerScope.GetInstance<ITestService>();
                    secondInnerScopeService = innerScope.GetInstance<ITestService>();
                }
                secondOuterScopeService = outerScope.GetInstance<ITestService>();
            }
            // assert
            outerScopeService.GetScopeIdentifier().Should().NotBe(innerScopeService.GetScopeIdentifier());
            secondOuterScopeService.GetScopeIdentifier().Should().NotBe(secondInnerScopeService.GetScopeIdentifier());
            outerScopeService.GetScopeIdentifier().Should().Be(secondOuterScopeService.GetScopeIdentifier());
            innerScopeService.GetScopeIdentifier().Should().Be(secondInnerScopeService.GetScopeIdentifier());
        }

        [Test]
        public void Dependency_injection_can_resolve_service_from_RegisterOpenGenericType()
        {
            var container = new ServiceCollection();
            container.AddScoped<ITestService, TestService>();
            container.AddSingleton(Mock.Of<ICountable>());

            container.RegisterGenericProcessor(typeof(TestCommandHandler), typeof(IProcessCommand<,>));

            var dependencyInjection = new MicrosoftDependencyInjection(container.BuildServiceProvider());

            var testHandler = dependencyInjection.GetScope().GetInstance<IProcessMessage<TestMessage, Task>>(typeof(IProcessCommand<TestMessage, TestMessageSettings>));
            testHandler.Should().NotBeNull();
        }
    }
}
