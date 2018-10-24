using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host
{
    public class StandardMessageProcessorProvider : IMessageProcessorProvider
    {
        private readonly ConcurrentDictionary<Type, object> _processors = new ConcurrentDictionary<Type, object>();

        public StandardMessageProcessorProvider RegisterProcessor(object processor)
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

        public IProcessMessage<T> GetProcessor<T>(Type type) where T : IMessage
        {
            return (IProcessMessage<T>) _processors[type];
        }

        public IEnumerable<Type> ListAllProcessors()
        {
            return _processors.Keys;
        }
    }
}