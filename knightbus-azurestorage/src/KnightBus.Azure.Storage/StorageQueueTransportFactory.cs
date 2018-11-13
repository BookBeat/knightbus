using System;
using System.Collections.Generic;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;

namespace KnightBus.Azure.Storage
{
    internal class StorageQueueTransportFactory : ITransportChannelFactory
    {
        public StorageQueueTransportFactory(IStorageBusConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ITransportConfiguration Configuration { get; set; }

        public IList<IMessageProcessorMiddleware> Middlewares { get; } = new List<IMessageProcessorMiddleware>();

        public IChannelReceiver Create(Type messageType, Type subscriptionType, Type settingsType, IHostConfiguration configuration, IMessageProcessor processor)
        {
            var settings = Activator.CreateInstance(settingsType);
            var queueReaderType = typeof(StorageQueueTransport<>).MakeGenericType(messageType);
            var queueReader = (IChannelReceiver)Activator.CreateInstance(queueReaderType, settings, processor, configuration, Configuration);
            return queueReader;
        }

        public bool CanCreate(Type messageType)
        {
            return typeof(IStorageQueueCommand).IsAssignableFrom(messageType);
        }
    }
}