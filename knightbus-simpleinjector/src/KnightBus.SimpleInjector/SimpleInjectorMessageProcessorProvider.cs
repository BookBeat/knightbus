using System;
using System.Collections.Generic;
using System.Linq;
using KnightBus.Core;
using KnightBus.Messages;
using SimpleInjector;

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
}