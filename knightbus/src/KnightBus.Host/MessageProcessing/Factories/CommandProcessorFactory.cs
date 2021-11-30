using System;
using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Host.MessageProcessing.Processors;

namespace KnightBus.Host.MessageProcessing.Factories
{
    internal class CommandProcessorFactory : IProcessorFactory
    {
        public ProcessorTypes GetProcessorTypes(Type processorInterface)
        {
            var messageType = processorInterface.GenericTypeArguments[0];
            var settingsType = processorInterface.GenericTypeArguments[1];
            return new ProcessorTypes(messageType, null, null, settingsType);
        }

        public IMessageProcessor GetProcessor(Type processorInterface)
        {
            var processorInstance = new MessageProcessor(processorInterface);
            return processorInstance;
        }

        public IEnumerable<Type> GetInterfaces(Type processorType)
        {
            return ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processorType, typeof(IProcessCommand<,>));
        }

        public bool CanCreate(Type processorInterface)
        {
            return processorInterface.IsGenericType && processorInterface.GetGenericTypeDefinition() == typeof(IProcessCommand<,>);
        }
    }
}