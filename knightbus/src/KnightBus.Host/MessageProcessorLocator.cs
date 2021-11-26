using System;
using System.Collections.Generic;
using System.Linq;
using KnightBus.Core;

namespace KnightBus.Host
{
    /// <summary>
    /// Locates what <see cref="IChannelReceiver"/> to create based on the registered processors.
    /// </summary>
    internal class MessageProcessorLocator
    {
        private readonly IHostConfiguration _configuration;
        private readonly TransportStarterFactory _transportStarterFactory;

        public MessageProcessorLocator(IHostConfiguration configuration, ITransportChannelFactory[] transportChannelFactories)
        {
            _configuration = configuration;
            _transportStarterFactory = new TransportStarterFactory(transportChannelFactories, configuration);
        }

        /// <summary>
        /// Creates all <see cref="IChannelReceiver"/> for the registered processors.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IChannelReceiver> CreateReceivers()
        {
            var processors = _configuration.DependencyInjection.GetOpenGenericRegistrations(typeof(IProcessMessage<>)).ToArray();
            return CreateCommandReceivers(processors)
                .Concat(CreateEventReceivers(processors))
                .Concat(CreateRequestReceivers()
                .Concat(CreateRequestStreamReceivers()));
        }

        private IEnumerable<IChannelReceiver> CreateCommandReceivers(IEnumerable<Type> processors)
        {
            foreach (var processor in processors)
            {
                var processorInterfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor, typeof(IProcessCommand<,>));
                foreach (var processorInterface in processorInterfaces)
                {
                    var messageType = processorInterface.GenericTypeArguments[0];
                    var settingsType = processorInterface.GenericTypeArguments[1];
                    
                    ConsoleWriter.WriteLine($"Found {processor.Name}<{messageType.Name}, {settingsType.Name}>");

                    yield return _transportStarterFactory.CreateChannelReceiver(messageType, null, null, processorInterface, settingsType, processor);
                }
            }
        }
        
        private IEnumerable<IChannelReceiver> CreateRequestReceivers()
        {
            var processors = _configuration.DependencyInjection.GetOpenGenericRegistrations(typeof(IProcessRequest<,>)).ToArray();
            foreach (var processor in processors)
            {
                var processorInterfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor, typeof(IProcessRequest<,,>));
                foreach (var processorInterface in processorInterfaces)
                {
                    var messageType = processorInterface.GenericTypeArguments[0];
                    var responseType = processorInterface.GenericTypeArguments[1];
                    var settingsType = processorInterface.GenericTypeArguments[2];
                    
                    ConsoleWriter.WriteLine($"Found {processor.Name}<{messageType.Name}, {responseType.Name}, {settingsType.Name}>");

                    yield return _transportStarterFactory.CreateChannelReceiver(messageType, responseType, null, processorInterface, settingsType, processor);
                }
            }
        }

        private IEnumerable<IChannelReceiver> CreateRequestStreamReceivers()
        {
            var processors = _configuration.DependencyInjection.GetOpenGenericRegistrations(typeof(IProcessRequest<,>)).ToArray();
            foreach (var processor in processors)
            {
                var processorInterfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor, typeof(IProcessStreamRequest<,,>));
                foreach (var processorInterface in processorInterfaces)
                {
                    var messageType = processorInterface.GenericTypeArguments[0];
                    var responseType = processorInterface.GenericTypeArguments[1];
                    var settingsType = processorInterface.GenericTypeArguments[2];

                    ConsoleWriter.WriteLine($"Found {processor.Name}<{messageType.Name}, {responseType.Name}, {settingsType.Name}>");

                    yield return _transportStarterFactory.CreateChannelReceiver(messageType, responseType, null, processorInterface, settingsType, processor);
                }
            }
        }

        private IEnumerable<IChannelReceiver> CreateEventReceivers(IEnumerable<Type> processors)
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
                    yield return _transportStarterFactory.CreateChannelReceiver(messageType, null, subscriptionType, processorInterface, settingsType, processor);
                }
            }
        }
    }
}