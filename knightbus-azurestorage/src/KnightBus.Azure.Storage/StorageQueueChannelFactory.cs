using System;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage
{
    internal class StorageQueueChannelFactory : ITransportChannelFactory
    {
        public StorageQueueChannelFactory(IStorageBusConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ITransportConfiguration Configuration { get; set; }

        public IChannelReceiver Create(Type messageType, IEventSubscription subscription, IProcessingSettings processingSettings, IMessageSerializer serializer, IHostConfiguration configuration, IMessageProcessor processor)
        {
            var queueReaderType = typeof(StorageQueueChannelReceiver<>).MakeGenericType(messageType);
            var queueReader = (IChannelReceiver)Activator.CreateInstance(queueReaderType, processingSettings, serializer, processor, configuration, Configuration);
            return queueReader;
        }

        public bool CanCreate(Type messageType)
        {
            return typeof(IStorageQueueCommand).IsAssignableFrom(messageType);
        }
    }
}
