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
}