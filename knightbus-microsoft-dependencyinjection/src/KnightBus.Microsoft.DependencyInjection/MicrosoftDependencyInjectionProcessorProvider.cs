using System;
using System.Collections.Generic;
using System.Linq;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace KnightBus.Microsoft.DependencyInjection
{
    public class MicrosoftDependencyInjectionProcessorProvider : IMessageProcessorProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceCollection _serviceCollection;

        public MicrosoftDependencyInjectionProcessorProvider(IServiceProvider serviceProvider, IServiceCollection serviceCollection)
        {
            _serviceProvider = serviceProvider;
            _serviceCollection = serviceCollection;
        }

        public IProcessMessage<T> GetProcessor<T>(Type type) where T : IMessage
        {
            return (IProcessMessage<T>)_serviceProvider.GetService(type);
        }

        public IEnumerable<Type> ListAllProcessors()
        {
            var allTypes = _serviceCollection.Select(x => x.ImplementationType).Where(t => t != null).Distinct();

            return ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(typeof(IProcessMessage<>), allTypes).Distinct();
        }
    }

    public class MicrosoftDependencyInjection : IDependencyInjection
    {
        private readonly IServiceProvider _provider;

        public MicrosoftDependencyInjection(IServiceProvider provider)
        {
            _provider = provider;
        }
        public IDisposable GetScope()
        {
            return _provider.CreateScope();
        }

        public T GetInstance<T>() where T : class
        {
            return _provider.GetService<T>();
        }

        public object GetInstance(Type type)
        {
            return _provider.GetService(type);
        }

        public IEnumerable<T> GetAllInstances<T>() where T : class
        {
            return _provider.GetServices<T>();
        }

        public IEnumerable<object> GetAllInstances(Type type)
        {
            return _provider.GetServices(type);
        }
    }
}