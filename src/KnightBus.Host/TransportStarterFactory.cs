using System;
using System.Linq;
using System.Threading;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using KnightBus.Host.MessageProcessing.Factories;
using KnightBus.Host.Singleton;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host;

internal class TransportStarterFactory
{
    private readonly ITransportChannelFactory[] _transportChannelFactories;
    private readonly IHostConfiguration _configuration;
    private readonly CancellationToken? _teardownToken;

    public TransportStarterFactory(
        ITransportChannelFactory[] transportChannelFactories,
        IHostConfiguration configuration,
        CancellationToken? teardownToken = null
    )
    {
        _transportChannelFactories = transportChannelFactories;
        _configuration = configuration;
        _teardownToken = teardownToken;
    }

    internal IChannelReceiver CreateChannelReceiver(
        IProcessorFactory processorFactory,
        Type processorInterface,
        Type processor
    )
    {
        IMessageProcessor processorInstance = processorFactory.GetProcessor(processorInterface);
        var processorTypes = processorFactory.GetProcessorTypes(processorInterface);

        var channelFactory = _transportChannelFactories.SingleOrDefault(factory =>
            factory.CanCreate(processorTypes.MessageType)
        );
        if (channelFactory == null)
            throw new TransportMissingException(processorTypes.MessageType);

        var processingSettings = (IProcessingSettings)
            Activator.CreateInstance(processorTypes.SettingsType)!;

        var eventSubscription =
            processorTypes.SubscriptionType == null
                ? null
                : (IEventSubscription)Activator.CreateInstance(processorTypes.SubscriptionType)!;
        var pipelineInformation = new PipelineInformation(
            processorInterface,
            eventSubscription,
            processingSettings,
            _configuration
        );

        var middlewares =
            _configuration.DependencyInjection.GetInstances<IMessageProcessorMiddleware>();
        var pipeline = new MiddlewarePipeline(middlewares, pipelineInformation, _configuration.Log);
        var serializer = GetSerializer(channelFactory, processorTypes.MessageType);
        var starter = channelFactory.Create(
            processorTypes.MessageType,
            eventSubscription,
            processingSettings,
            serializer,
            _configuration,
            pipeline.GetPipeline(processorInstance)
        );
        return WrapSingletonReceiver(starter, processor, eventSubscription);
    }

    private IMessageSerializer GetSerializer(
        ITransportChannelFactory channelFactory,
        Type messageType
    )
    {
        var mapping = AutoMessageMapper.GetMapping(messageType);
        if (mapping is ICustomMessageSerializer serializer)
            return serializer.MessageSerializer;

        return channelFactory.Configuration.MessageSerializer;
    }

    private IChannelReceiver WrapSingletonReceiver(
        IChannelReceiver channelReceiver,
        Type type,
        IEventSubscription? subscription
    )
    {
        if (typeof(ISingletonProcessor).IsAssignableFrom(type))
        {
            var lockManager =
                _configuration.DependencyInjection.GetInstance<ISingletonLockManager>();
            _configuration.Log.LogInformation("Setting {SettingName} in Singleton mode", type.Name);

            var lockId = channelReceiver.GetType().FullName;
            if (!string.IsNullOrWhiteSpace(subscription?.Name))
            {
                lockId = $"{lockId}:{subscription.Name}";
            }

            var singletonStarter = new SingletonChannelReceiver(
                channelReceiver,
                lockManager,
                _configuration.Log,
                lockId,
                _teardownToken
            );
            return singletonStarter;
        }

        return channelReceiver;
    }
}
