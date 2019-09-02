using System;
using System.Collections.Generic;
using System.Linq;
using KnightBus.Core;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace KnightBus.SimpleInjector
{
    public class SimpleInjectorDependencyInjection : IDependencyInjection
    {
        private readonly Container _container;

        public SimpleInjectorDependencyInjection(Container container)
        {
            _container = container;
        }

        public IDisposable GetScope()
        {
            return AsyncScopedLifestyle.BeginScope(_container);
        }

        public T GetInstance<T>() where T : class
        {
            return _container.GetInstance<T>();
        }

        public T GetInstance<T>(Type type)
        {
            return (T) _container.GetInstance(type);
        }

        public IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric)
        {
            var allTypes = _container.GetCurrentRegistrations()
                .Select(x => x.Registration.ImplementationType)
                .Distinct();
            return ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(openGeneric, allTypes).Distinct();
        }
    }
}