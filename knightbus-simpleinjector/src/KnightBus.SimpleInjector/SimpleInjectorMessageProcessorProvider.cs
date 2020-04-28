using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KnightBus.Core;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace KnightBus.SimpleInjector
{
    public class SimpleInjectorDependencyInjection : IDependencyInjection
    {
        private readonly Container _container;
        private readonly Scope _scope;


        public SimpleInjectorDependencyInjection(Container container, Scope scope = null)
        {
            _container = container;
            _scope = scope;
        }

        public IDependencyInjection GetScope()
        {
            return new SimpleInjectorDependencyInjection(_container, AsyncScopedLifestyle.BeginScope(_container));
        }

        public T GetInstance<T>() where T : class
        {
            return _container.GetInstance<T>();
        }

        public T GetInstance<T>(Type type)
        {
            return (T)_container.GetInstance(type);
        }

        public IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric)
        {
            var allTypes = _container.GetCurrentRegistrations()
                .Select(x => x.Registration.ImplementationType)
                .Distinct();
            return ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(openGeneric, allTypes).Distinct();
        }

        public void RegisterOpenGeneric(Type openGeneric, Assembly assembly)
        {
            _container.Register(openGeneric, new List<Assembly> { assembly }, Lifestyle.Scoped);
        }

        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}