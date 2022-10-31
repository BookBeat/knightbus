using System;
using System.Linq;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using KnightBus.Host.MessageProcessing.Factories;
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

        internal IChannelReceiver CreateChannelReceiver(IProcessorFactory processorFactory, Type processorInterface, Type processor)
        {

            IMessageProcessor processorInstance = processorFactory.GetProcessor(processorInterface);
            var processorTypes = processorFactory.GetProcessorTypes(processorInterface);

            var channelFactory = _transportChannelFactories.SingleOrDefault(factory => factory.CanCreate(processorTypes.MessageType));
            if (channelFactory == null) throw new TransportMissingException(processorTypes.MessageType);

            var processingSettings = (IProcessingSettings)Activator.CreateInstance(processorTypes.SettingsType);

            var eventSubscription = processorTypes.SubscriptionType == null ? null : (IEventSubscription)Activator.CreateInstance(processorTypes.SubscriptionType);
            var pipelineInformation = new PipelineInformation(processorInterface, eventSubscription, processingSettings, _configuration);

            var pipeline = new MiddlewarePipeline(_configuration.Middlewares, pipelineInformation, channelFactory, _configuration.Log);
            var serializer = GetSerializer(channelFactory, processorTypes.MessageType);
            var starter = channelFactory.Create(processorTypes.MessageType, eventSubscription, processingSettings, serializer, _configuration, pipeline.GetPipeline(processorInstance));
            return WrapSingletonReceiver(starter, processor);
        }

        private IMessageSerializer GetSerializer(ITransportChannelFactory channelFactory, Type messageType)
        {
            var mapping = AutoMessageMapper.GetMapping(messageType);
            if (mapping is ICustomMessageSerializer serializer) return serializer.MessageSerializer;

            return channelFactory.Configuration.MessageSerializer;
        }

        private IChannelReceiver WrapSingletonReceiver(IChannelReceiver channelReceiver, Type type)
        {
            if (typeof(ISingletonProcessor).IsAssignableFrom(type))
            {
                var lockManager = _configuration.DependencyInjection.GetInstance<ISingletonLockManager>();
                ConsoleWriter.WriteLine($"Setting {type.Name} in Singleton mode");
                var singletonStarter = new SingletonChannelReceiver(channelReceiver, lockManager, _configuration.Log);
                return singletonStarter;
            }

            return channelReceiver;
        }
    }
}