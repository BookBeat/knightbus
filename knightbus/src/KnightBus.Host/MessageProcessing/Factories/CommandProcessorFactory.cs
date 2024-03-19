using System;
using KnightBus.Core;
using KnightBus.Host.MessageProcessing.Processors;

namespace KnightBus.Host.MessageProcessing.Factories;

internal class CommandProcessorFactory : MessageProcessorFactoryBase, IProcessorFactory
{
    public CommandProcessorFactory() : base(typeof(IProcessCommand<,>))
    { }

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
}
