using System;
using System.Collections.Generic;
using System.Linq;
using KnightBus.Core;
using KnightBus.Messages;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace KnightBus.SimpleInjector
{
    public class SimpleInjectorMessageProcessorProvider : IMessageProcessorProvider
    {
        private readonly Container _container;

        public SimpleInjectorMessageProcessorProvider(Container container)
        {
            _container = container;
        }

        public IProcessMessage<T> GetProcessor<T>(Type type) where T : IMessage
        {
            return (IProcessMessage<T>)_container.GetInstance(type);
        }

        public IEnumerable<Type> ListAllProcessors()
        {
            var allTypes = _container.GetCurrentRegistrations()
                .Select(x => x.Registration.ImplementationType)
                .Distinct();
            return ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(typeof(IProcessMessage<>), allTypes).Distinct();
        }
    }

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

        public object GetInstance(Type type)
        {
            return _container.GetInstance(type);
        }

        public IEnumerable<T> GetAllInstances<T>() where T : class
        {
            return _container.GetAllInstances<T>();
        }

        public IEnumerable<T> GetAllInstances<T>(Type type)
        {
            return _container.GetAllInstances(type).Select(x=> (T)x);
        }
    }
}