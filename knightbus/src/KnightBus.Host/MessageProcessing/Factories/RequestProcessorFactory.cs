using System;
using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Host.MessageProcessing.Processors;

namespace KnightBus.Host.MessageProcessing.Factories
{
    internal class RequestProcessorFactory : IProcessorFactory
    {
        public ProcessorTypes GetProcessorTypes(Type processorInterface)
        {
            var messageType = processorInterface.GenericTypeArguments[0];
            var requestType = processorInterface.GenericTypeArguments[1];
            var settingsType = processorInterface.GenericTypeArguments[2];
            return new ProcessorTypes(messageType, requestType, null, settingsType);
        }

        public IMessageProcessor GetProcessor(Type processorInterface)
        {
            var requestProcessorType = typeof(RequestProcessor<>).MakeGenericType(GetProcessorTypes(processorInterface).ResponseType);
            return (IMessageProcessor)Activator.CreateInstance(requestProcessorType, processorInterface);
        }

        public IEnumerable<Type> GetInterfaces(Type processorType)
        {
            return ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processorType, typeof(IProcessRequest<,,>));
        }

        public bool CanCreate(Type processorInterface)
        {
            return processorInterface.IsGenericType && processorInterface.GetGenericTypeDefinition() == typeof(IProcessRequest<,,>);
        }
    }
}