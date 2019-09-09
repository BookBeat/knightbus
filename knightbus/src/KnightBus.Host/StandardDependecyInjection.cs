using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KnightBus.Core;

namespace KnightBus.Host
{
    /// <summary>
    /// <see cref="IDependencyInjection"/> when you don't need IoC. Will treat all <see cref="IProcessMessage{T}"/> as singletons.
    /// </summary>
    public class StandardDependecyInjection : IDependencyInjection
    {
        private readonly ConcurrentDictionary<Type, object> _processors = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// Adds a <see cref="IProcessCommand{T,TSettings}"/> or <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/> to the provider
        /// </summary>
        /// <param name="processor"><see cref="IProcessCommand{T,TSettings}"/> or <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/></param>
        public StandardDependecyInjection RegisterProcessor(object processor)
        {
            Register(processor, typeof(IProcessCommand<,>));
            Register(processor, typeof(IProcessEvent<,,>));
            return this;
        }

        private void Register(object processor, Type type)
        {
            var processorInterfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor.GetType(), type);
            foreach (var processorInterface in processorInterfaces)
            {
                _processors[processorInterface] = processor;
            }
        }

        public IDisposable GetScope()
        {
            return new SingletonScope();
        }

        public T GetInstance<T>() where T : class
        {
            return (T)_processors[typeof(T)];
        }

        public T GetInstance<T>(Type type)
        {
            return (T)_processors[type];
        }

        public IEnumerable<Type> GetOpenGenericRegistrations(Type openGeneric)
        {
            return _processors.Values.Select(x => x.GetType()).Distinct();
        }

        public void RegisterOpenGeneric(Type openGeneric, Assembly assembly)
        {
            
        }
    }

    public class SingletonScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}