using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host
{
    /// <summary>
    /// <see cref="IMessageProcessorProvider"/> when you don't need IoC. Will treat all <see cref="IProcessMessage{T}"/> as singletons.
    /// </summary>
    public class StandardMessageProcessorProvider : IMessageProcessorProvider
    {
        private readonly ConcurrentDictionary<Type, object> _processors = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// Adds a <see cref="IProcessCommand{T,TSettings}"/> or <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/> to the provider
        /// </summary>
        /// <param name="processor"><see cref="IProcessCommand{T,TSettings}"/> or <see cref="IProcessEvent{TTopic,TTopicSubscription,TSettings}"/></param>
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