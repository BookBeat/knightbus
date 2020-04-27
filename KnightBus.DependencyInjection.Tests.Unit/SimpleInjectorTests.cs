using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.SimpleInjector;
using NUnit.Framework;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using FluentAssertions;

namespace KnightBus.DependencyInjection.Tests.Unit
{
    public class SimpleInjectorTests
    {
        private IDependencyInjection DependencyInjection { get; set; }

        [SetUp]
        public void Setup()
        {
            var container = new Container { Options = { DefaultScopedLifestyle = new AsyncScopedLifestyle() } };
            container.Register<ITestService, TestService>(Lifestyle.Scoped);

            DependencyInjection = new SimpleInjectorDependencyInjection(container);
            container.Verify();
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
