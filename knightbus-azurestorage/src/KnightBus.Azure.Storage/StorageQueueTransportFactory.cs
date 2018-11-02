using System;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;

namespace KnightBus.Azure.Storage
{
    internal class StorageQueueTransportFactory : ITransportFactory
    {
        public StorageQueueTransportFactory(IStorageBusConfiguration configuration)
        {
            Configuration = configuration;
        }

        public ITransportConfiguration Configuration { get; }

        public IStartTransport Create(Type messageType, Type subscriptionType, Type settingsType, IHostConfiguration configuration, IMessageProcessor processor)
        {
            var settings = Activator.CreateInstance(settingsType);
            var queueReaderType = typeof(StorageQueueTransport<,>).MakeGenericType(messageType, settingsType);
            var queueReader = (IStartTransport)Activator.CreateInstance(queueReaderType, settings, processor, configuration, Configuration);
            return queueReader;
        }

        public bool CanCreate(Type messageType)
        {
            return typeof(IStorageQueueCommand).IsAssignableFrom(messageType);
        }
    }
}