using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Microsoft.DependencyInjection;
using KnightBus.Schedule;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace KnightBus.DependencyInjection.Tests.Unit
{
    public class TestSchedule : ISchedule
    {
        public string CronExpression { get; }
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

            DependencyInjection = new MicrosoftDependencyInjection(container);
            DependencyInjection.Build();
        }

        [Test]
        public void DependencyInjection_can_register_open_generic()
        {
            //arrange
            var container = new ServiceCollection();
            var di = new MicrosoftDependencyInjection(container);
            di.RegisterSchedules(typeof(TestSchedule).Assembly);

            di = new MicrosoftDependencyInjection(container);
            di.Build();
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
            container.AddSingleton<ICountable>(Mock.Of<ICountable>());

            MicrosoftDependencyInjectionExtensions.RegisterOpenGenericType(container, typeof(TestCommandHandler), typeof(IProcessCommand<,>));

            var dependencyInjection = new MicrosoftDependencyInjection(container);
            dependencyInjection.Build();

            var testHandler = dependencyInjection.GetScope().GetInstance<IProcessMessage<TestMessage>>(typeof(IProcessCommand<TestMessage, TestMessageSettings>));
            testHandler.Should().NotBeNull();
        }
    }
}