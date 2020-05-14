using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KnightBus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Microsoft.DependencyInjection
{
    public class MicrosoftDependencyInjection : IDependencyInjection
    {
        private readonly IServiceProvider _provider;
        private readonly IServiceCollection _serviceCollection;
        private readonly IServiceScope _scope;

        public MicrosoftDependencyInjection(IServiceProvider provider, IServiceCollection serviceCollection, IServiceScope scope = null)
        {
            _provider = provider;
            _serviceCollection = serviceCollection;
            _scope = scope;
        }
        public IDependencyInjection GetScope()
        {
            var scope = _provider.CreateScope();
            return new MicrosoftDependencyInjection(scope.ServiceProvider, _serviceCollection, scope);
        }

        public T GetInstance<T>() where T : class
        {
            return _provider.GetService<T>();
        }

        public T GetInstance<T>(Type type)
        {
            return (T)_provider.GetService(type);
        }

        public IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric)
        {
            var allTypes = _serviceCollection.Select(x => x.ImplementationType).Where(t => t != null).Distinct();

            return ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(openGeneric, allTypes).Distinct();
        }

        public void RegisterOpenGeneric(Type openGeneric, Assembly assembly)
        {
            foreach (var command in ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(typeof(IProcessCommand<,>), assembly))
                _serviceCollection.AddScoped(openGeneric, command);
        }

        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}