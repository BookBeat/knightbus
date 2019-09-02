using System;
using System.Collections.Generic;
using System.Linq;
using KnightBus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Microsoft.DependencyInjection
{
    public class MicrosoftDependencyInjection : IDependencyInjection
    {
        private readonly IServiceProvider _provider;
        private readonly IServiceCollection _serviceCollection;

        public MicrosoftDependencyInjection(IServiceProvider provider, IServiceCollection serviceCollection)
        {
            _provider = provider;
            _serviceCollection = serviceCollection;
        }
        public IDisposable GetScope()
        {
            return _provider.CreateScope();
        }

        public T GetInstance<T>() where T : class
        {
            return _provider.GetService<T>();
        }

        public T GetInstance<T>(Type type)
        {
            return (T) _provider.GetService(type);
        }

        public IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric)
        {
            var allTypes = _serviceCollection.Select(x => x.ImplementationType).Where(t => t != null).Distinct();

            return ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(openGeneric, allTypes).Distinct();
        }
    }
}