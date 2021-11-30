using System;
using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Host.MessageProcessing.Processors;

namespace KnightBus.Host.MessageProcessing.Factories
{
    internal class EventProcessorFactory : IProcessorFactory
    {
        public ProcessorTypes GetProcessorTypes(Type processorInterface)
        {
            var messageType = processorInterface.GenericTypeArguments[0];
            var subscriptionType = processorInterface.GenericTypeArguments[1];
            var settingsType = processorInterface.GenericTypeArguments[2];
            return new ProcessorTypes(messageType, null, subscriptionType, settingsType);
        }

        public IMessageProcessor GetProcessor(Type processorInterface)
        {
            var processorInstance = new MessageProcessor(processorInterface);
            return processorInstance;
        }

        public IEnumerable<Type> GetInterfaces(Type processorType)
        {
            return ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processorType, typeof(IProcessEvent<,,>));
        }

        public bool CanCreate(Type processorInterface)
        {
            return processorInterface.IsGenericType && processorInterface.GetGenericTypeDefinition() == typeof(IProcessEvent<,,>);
        }
    }
}