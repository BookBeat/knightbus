using System;
using System.Collections.Generic;
using System.Linq;
using KnightBus.Core;
using KnightBus.Host.MessageProcessing.Factories;

namespace KnightBus.Host.MessageProcessing
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
            var processors = _configuration.DependencyInjection.GetOpenGenericRegistrations(typeof(IProcessMessage<,>));
            return CreateCommandReceivers(processors);
        }

        private IEnumerable<IChannelReceiver> CreateCommandReceivers(IEnumerable<Type> processors)
        {
            var factories = new List<IProcessorFactory> { new CommandProcessorFactory(), new EventProcessorFactory(), new RequestProcessorFactory(), new StreamRequestProcessorFactory() };
            foreach (var processor in processors)
            {
                var processorInterfaces = factories.SelectMany(x => x.GetInterfaces(processor));
                foreach (var processorInterface in processorInterfaces)
                {
                    var factory = factories.SingleOrDefault(x => x.CanCreate(processorInterface));
                    
                    //ConsoleWriter.WriteLine($"Found {processor.Name}<{messageType.Name}, {settingsType.Name}>");
                    if(factory!= null)
                        yield return _transportStarterFactory.CreateChannelReceiver(factory, processorInterface, processor);
                }
            }
        }
    }
}