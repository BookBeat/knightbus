using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Microsoft.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace KnightBus.DependencyInjection.Tests.Unit
{
    public class MicrosoftDependencyInjectionTests
    {
        private IDependencyInjection DependencyInjection { get; set; }

        [SetUp]
        public void Setup()
        {
            var container = new ServiceCollection();
            container.AddScoped<ITestService, TestService>();
            var serviceProvider = new DefaultServiceProviderFactory().CreateServiceProvider(container);

            DependencyInjection = new MicrosoftDependencyInjection(serviceProvider, container);
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
    }
}