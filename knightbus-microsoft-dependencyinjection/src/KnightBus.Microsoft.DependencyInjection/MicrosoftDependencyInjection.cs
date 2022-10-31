using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Microsoft.DependencyInjection
{
    public class MicrosoftDependencyInjection : IDependencyInjection
    {
        private IServiceProvider _provider;
        private readonly IServiceScope _scope;

        public MicrosoftDependencyInjection(IServiceProvider provider, IServiceScope scope = null)
        {
            _provider = provider;
            _scope = scope;
        }

        
        public IDependencyInjection GetScope()
        {
            var scope = _provider.CreateScope();
            return new MicrosoftDependencyInjection(scope.ServiceProvider, scope);
        }

        public T GetInstance<T>() where T : class
        {
            return _provider.GetRequiredService<T>();
        }

        public T GetInstance<T>(Type type)
        {
            return (T)_provider.GetRequiredService(type);
        }

        public IEnumerable<T> GetInstances<T>()
        {
            return _provider.GetServices<T>();
        }

        public IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric)
        {
            var allTypes = _provider.GetServices<IGenericProcessor>().Distinct().Select(o=> o.GetType());
            return ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(openGeneric, allTypes).Distinct();
        }

        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}
