using System;
using System.Collections.Generic;
using System.Linq;
using KnightBus.Core;

namespace KnightBus.Host
{
    internal class MessageProcessorLocator
    {
        private readonly IHostConfiguration _configuration;
        private readonly TransportStarterFactory _transportStarterFactory;

        public MessageProcessorLocator(IHostConfiguration configuration, ITransportChannelFactory[] transportChannelFactories)
        {
            _configuration = configuration;
            _transportStarterFactory = new TransportStarterFactory(transportChannelFactories, configuration);
        }

        public IEnumerable<IChannelReceiver> Locate()
        {
            var processors = _configuration.DependencyInjection.GetOpenGenericRegistrations(typeof(IProcessMessage<>));
            return GetCommandReceivers(processors)
                .Concat(GetEventReceivers(processors));
        }

        private IEnumerable<IChannelReceiver> GetCommandReceivers(IEnumerable<Type> processors)
        {
            foreach (var processor in processors)
            {
                var processorInterfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor, typeof(IProcessCommand<,>));
                foreach (var processorInterface in processorInterfaces)
                {
                    var messageType = processorInterface.GenericTypeArguments[0];
                    var settingsType = processorInterface.GenericTypeArguments[1];
                    
                    ConsoleWriter.WriteLine($"Found {processor.Name}<{messageType.Name}, {settingsType.Name}>");

                    yield return _transportStarterFactory.CreateChannelReceiver(messageType, null, processorInterface, settingsType, processor);
                }
            }
        }

        private IEnumerable<IChannelReceiver> GetEventReceivers(IEnumerable<Type> processors)
        {
            foreach (var processor in processors)
            {
                var processorInterfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor, typeof(IProcessEvent<,,>));
                foreach (var processorInterface in processorInterfaces)
                {
                    var messageType = processorInterface.GenericTypeArguments[0];
                    var subscriptionType = processorInterface.GenericTypeArguments[1];
                    var settingsType = processorInterface.GenericTypeArguments[2];

                    ConsoleWriter.WriteLine($"Found {processor.Name}<{messageType.Name}, {subscriptionType.Name}, {settingsType.Name}>");
                    yield return _transportStarterFactory.CreateChannelReceiver(messageType, subscriptionType, processorInterface, settingsType, processor);
                }
            }
        }
    }
}