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

        public void RegisterProcessor<T>(IProcessMessage<T> processor) where T : IMessage
        {
            _processors[typeof(T)] = processor;
        }


        public IProcessMessage<T> GetProcessor<T>(Type type) where T : IMessage
        {
            return (IProcessMessage<T>) _processors[typeof(T)];
        }

        public IEnumerable<Type> ListAllProcessors()
        {
            return _processors.Keys;
        }
    }
}