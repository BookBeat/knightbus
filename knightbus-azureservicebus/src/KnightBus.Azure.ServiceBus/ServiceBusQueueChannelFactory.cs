using System;
using System.Collections.Generic;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;

namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusQueueChannelFactory : ITransportChannelFactory
    {
        public ServiceBusQueueChannelFactory(IServiceBusConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ITransportConfiguration Configuration { get; set; }
        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();

        public IChannelReceiver Create(Type messageType, Type subscriptionType, Type settingsType, IHostConfiguration configuration, IMessageProcessor processor)
        {
            var settings = Activator.CreateInstance(settingsType);
            var queueReaderType = typeof(ServiceBusQueueTransport<>).MakeGenericType(messageType);
            var queueReader = (IChannelReceiver)Activator.CreateInstance(queueReaderType, settings, Configuration, configuration, processor);
            return queueReader;
        }

        
        public bool CanCreate(Type messageType)
        {
            return typeof(IServiceBusCommand).IsAssignableFrom(messageType);
        }
    }
}