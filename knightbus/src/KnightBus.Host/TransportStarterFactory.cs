using System;
using System.Linq;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Core.Singleton;
using KnightBus.Host.Singleton;
using KnightBus.Messages;

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

        internal IChannelReceiver CreateChannelReceiver(Type messageType, Type responseType, Type subscriptionType, Type processorInterface, Type settingsType, Type processor)
        {

            IMessageProcessor processorInstance;
            if (responseType != null)
            {
                var requestProcessorType = typeof(RequestProcessor<>).MakeGenericType(responseType);
                processorInstance = (IMessageProcessor)Activator.CreateInstance(requestProcessorType, processorInterface);
            }
            else
            {
                processorInstance = new MessageProcessor(processorInterface);
            }
            var channelFactory = _transportChannelFactories.SingleOrDefault(factory => factory.CanCreate(messageType));
            if (channelFactory == null) throw new TransportMissingException(messageType);

            var processingSettings = (IProcessingSettings)Activator.CreateInstance(settingsType);

            var eventSubscription = subscriptionType == null ? null : (IEventSubscription)Activator.CreateInstance(subscriptionType);
            var pipelineInformation = new PipelineInformation(processorInterface, eventSubscription, processingSettings, _configuration);

            var pipeline = new MiddlewarePipeline(_configuration.Middlewares, pipelineInformation, channelFactory, _configuration.Log);
            var serializer = GetSerializer(channelFactory, messageType);
            var starter = channelFactory.Create(messageType, eventSubscription, processingSettings, serializer, _configuration, pipeline.GetPipeline(processorInstance));
            return WrapSingletonReceiver(starter, processor);
        }

        private IMessageSerializer GetSerializer(ITransportChannelFactory channelFactory, Type messageType)
        {
            var mapping = AutoMessageMapper.GetMapping(messageType);
            if(mapping is ICustomMessageSerializer serializer) return serializer.MessageSerializer;

            return channelFactory.Configuration.MessageSerializer;
        }

        private IChannelReceiver WrapSingletonReceiver(IChannelReceiver channelReceiver, Type type)
        {
            if (typeof(ISingletonProcessor).IsAssignableFrom(type))
            {
                if (_configuration.SingletonLockManager == null)
                    throw new SingletonLockManagerMissingException("There is no ISingletonLockManager specified, you cannot use the ISingletonProcessor directive without one");
                ConsoleWriter.WriteLine($"Setting {type.Name} in Singleton mode");
                var singletonStarter = new SingletonChannelReceiver(channelReceiver, _configuration.SingletonLockManager, _configuration.Log);
                return singletonStarter;
            }

            return channelReceiver;
        }
    }
}