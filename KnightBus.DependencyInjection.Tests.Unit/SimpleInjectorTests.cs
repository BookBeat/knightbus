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
            var container = new Container { Options = { DefaultScopedLifestyle = ScopedLifestyle.Flowing } };
            container.Register<ITestService, TestService>(Lifestyle.Scoped);

            DependencyInjection = new SimpleInjectorDependencyInjection(container);
            container.Verify();
        }

        [Test] // needs to be run async since SimpleInjectorDependencyInjection creates async scopes
        public async Task DependencyInjection_resolves_services_in_correct_scope()
        {
            await Task.Run(() => // removes annoying warning about async not using awaiter
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
            });
        }
    }
}
