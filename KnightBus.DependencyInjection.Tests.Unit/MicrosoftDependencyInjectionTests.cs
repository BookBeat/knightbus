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
            var serviceProvider = new DefaultServiceProviderFactory().CreateServiceProvider(container);
            container.AddScoped<ITestService, TestService>();

            DependencyInjection = new MicrosoftDependencyInjection(serviceProvider, container);
        }

        [Test]
        public async Task Scoped_Instance_Is_Different_From_Default()
        {
            // arrange
            ITestService outerScopeService;
            ITestService innerScopeService;
            ITestService secondInnerScopeService;
            ITestService secondOuterScopeService;

            // act
            using (DependencyInjection.GetScope())
            {
                outerScopeService = DependencyInjection.GetInstance<ITestService>();

                using (DependencyInjection.GetScope())
                {
                    innerScopeService = DependencyInjection.GetInstance<ITestService>();
                    secondInnerScopeService = DependencyInjection.GetInstance<ITestService>();
                }
                secondOuterScopeService = DependencyInjection.GetInstance<ITestService>();
            }

            // assert
            outerScopeService.GetScopeIdentifier().Should().NotBe(innerScopeService.GetScopeIdentifier());
            outerScopeService.GetScopeIdentifier().Should().Be(secondOuterScopeService.GetScopeIdentifier());
            innerScopeService.GetScopeIdentifier().Should().Be(secondInnerScopeService.GetScopeIdentifier());
        }
    }
}