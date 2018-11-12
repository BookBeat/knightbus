using System;
using System.Collections.Generic;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;

namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusTopicTransportFactory : ITransportChannelFactory
    {
        public ServiceBusTopicTransportFactory(IServiceBusConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ITransportConfiguration Configuration { get; set; }
        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();

        public IChannelReceiver Create(Type messageType, Type subscriptionType, Type settingsType, IHostConfiguration configuration, IMessageProcessor processor)
        {
            var settings = Activator.CreateInstance(settingsType);
            var topicSubscription = Activator.CreateInstance(subscriptionType);
            var queueReaderType = typeof(ServiceBusTopicTransport<,>).MakeGenericType(messageType, settingsType);
            var queueReader = (IChannelReceiver)Activator.CreateInstance(queueReaderType, settings, topicSubscription, Configuration, configuration, processor);
            return queueReader;
        }

        public bool CanCreate(Type messageType)
        {
            return typeof(IServiceBusEvent).IsAssignableFrom(messageType);
        }
    }
}