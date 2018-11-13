using System;
using System.Linq;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using KnightBus.Host.Singleton;

namespace KnightBus.Host
{
    internal class TransportStarterFactory
    {
        private readonly ITransportChannelFactory[] _transportChannelFactories;
        private readonly IHostConfiguration _configuration;
        
        public TransportStarterFactory(ITransportChannelFactory[] transportChannelFactories, IHostConfiguration configuration)
        {
            _transportChannelFactories = transportChannelFactories;
            _configuration = configuration;
        }

        internal IChannelReceiver CreateQueueReader(Type messageType, Type subscriptionType, Type processorInterface, Type settingsType, Type processor)
        {
            var messageProcessorType = typeof(MessageProcessor<>).MakeGenericType(processorInterface);
            var processorInstance = (IMessageProcessor)Activator.CreateInstance(messageProcessorType, _configuration.MessageProcessorProvider);
            var queueReader = _transportChannelFactories.SingleOrDefault(factory => factory.CanCreate(messageType));
            if (queueReader == null) throw new TransportMissingException(messageType);

            var pipeline = new MiddlewarePipeline(_configuration.Middlewares, queueReader, _configuration.Log);

            var starter = queueReader.Create(messageType, subscriptionType, settingsType, _configuration, pipeline.GetPipeline(processorInstance));
            return GetQueueReaderStarter(starter, processor);
        }

        private IChannelReceiver GetQueueReaderStarter(IChannelReceiver channelReceiver, Type type)
        {
            if (typeof(ISingletonProcessor).IsAssignableFrom(type))
            {
                if (_configuration.SingletonLockManager == null) throw new SingletonLockManagerMissingException();
                Console.WriteLine($"Setting {type.Name} in Singleton mode\n");
                var singletonStarter = new SingletonChannelReceiver(channelReceiver, _configuration.SingletonLockManager, _configuration.Log);
                return singletonStarter;
            }

            Console.WriteLine();
            return channelReceiver;
        }
    }
}