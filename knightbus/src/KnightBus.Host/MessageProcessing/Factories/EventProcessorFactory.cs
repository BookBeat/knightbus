using System;
using KnightBus.Core;
using KnightBus.Host.MessageProcessing.Processors;

namespace KnightBus.Host.MessageProcessing.Factories
{
    internal class EventProcessorFactory : MessageProcessorFactoryBase, IProcessorFactory
    {
        public EventProcessorFactory() : base(typeof(IProcessEvent<,,>))
        { }

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
    }
}
