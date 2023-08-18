using System;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Nats.Messages;

namespace KnightBus.Nats
{
    public class JetStreamChannelFactory : ITransportChannelFactory
    {
        public JetStreamChannelFactory(IJetStreamConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ITransportConfiguration Configuration { get; set; }

        public IChannelReceiver Create(Type messageType, IEventSubscription subscription, IProcessingSettings processingSettings,
            IMessageSerializer serializer, IHostConfiguration configuration, IMessageProcessor processor)
        {

            var readerType = typeof(JetStreamQueueChannelReceiver<>).MakeGenericType(messageType);
            var reader = (IChannelReceiver)Activator.CreateInstance(readerType, processingSettings, serializer, configuration, processor, Configuration, subscription);

            return reader;
        }

        public bool CanCreate(Type messageType)
        {
            return typeof(IJetStreamCommand).IsAssignableFrom(messageType) || typeof(IJetStreamEvent).IsAssignableFrom(messageType) || typeof(IJetStreamRequest).IsAssignableFrom(messageType);
        }
    }
}
