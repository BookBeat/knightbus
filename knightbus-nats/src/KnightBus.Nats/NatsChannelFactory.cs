using System;
using System.Collections.Generic;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Nats.Messages;

namespace KnightBus.Nats
{
    public class NatsChannelFactory : ITransportChannelFactory
    {
        public NatsChannelFactory(INatsBusConfiguration configuration)
        {
            Configuration = configuration;
        }
        
        public ITransportConfiguration Configuration { get; set; }
        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();

        public IChannelReceiver Create(Type messageType, IEventSubscription subscription, IProcessingSettings processingSettings,
            IMessageSerializer serializer, IHostConfiguration configuration, IMessageProcessor processor)
        {

            var readerType = typeof(NatsQueueChannelReceiver<>).MakeGenericType(messageType);
            var reader = (IChannelReceiver) Activator.CreateInstance(readerType, processingSettings, serializer,
                configuration, processor, Configuration);

            return reader;
        }

        public bool CanCreate(Type messageType)
        {
            return typeof(INatsCommand).IsAssignableFrom(messageType) || typeof(INatsEvent).IsAssignableFrom(messageType) || typeof(INatsRequest).IsAssignableFrom(messageType); ;
        }
    }
}