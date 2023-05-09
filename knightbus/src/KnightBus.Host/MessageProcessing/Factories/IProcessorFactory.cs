using System;
using System.Collections.Generic;
using KnightBus.Core;

namespace KnightBus.Host.MessageProcessing.Factories
{
    internal interface IProcessorFactory
    {
        ProcessorTypes GetProcessorTypes(Type processorInterface);
        IMessageProcessor GetProcessor(Type processorInterface);
        public IEnumerable<Type> GetInterfaces(Type processorType);
        bool CanCreate(Type processorInterface);
    }
}
