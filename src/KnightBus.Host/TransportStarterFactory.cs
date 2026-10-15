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
    private readonly ISingletonPlacement? _placement;

    public TransportStarterFactory(
        ITransportChannelFactory[] transportChannelFactories,
        IHostConfiguration configuration,
        CancellationToken? teardownToken = null,
        ISingletonPlacement? placement = null
    )
    {
        _transportChannelFactories = transportChannelFactories;
        _configuration = configuration;
        _teardownToken = teardownToken;
        _placement = placement;
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
        var messageProcessor = pipeline.GetPipeline(processorInstance);
        //A singleton keeps its lock until the messages it is handling have stopped
        var inFlight = typeof(ISingletonProcessor).IsAssignableFrom(processor)
            ? new InFlightMessages(messageProcessor)
            : null;
        var starter = channelFactory.Create(
            processorTypes.MessageType,
            eventSubscription,
            processingSettings,
            serializer,
            _configuration,
            inFlight ?? messageProcessor
        );
        if (inFlight == null)
            return starter;
        return WrapSingletonReceiver(
            starter,
            processor,
            eventSubscription,
            processingSettings,
            inFlight
        );
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

    private static int WeightOf(IProcessingSettings settings, Type processor)
    {
        if (settings is not ISingletonWeight weighted)
            return 1;
        if (weighted.Weight < 1)
            throw new InvalidOperationException(
                $"The singleton weight of {processor.Name} must be at least 1, it is {weighted.Weight}"
            );
        return weighted.Weight;
    }

    private IChannelReceiver WrapSingletonReceiver(
        IChannelReceiver channelReceiver,
        Type type,
        IEventSubscription? subscription,
        IProcessingSettings processingSettings,
        InFlightMessages inFlight
    )
    {
        var lockManager = _configuration.DependencyInjection.GetInstance<ISingletonLockManager>();
        _configuration.Log.LogInformation("Setting {SettingName} in Singleton mode", type.Name);

        var lockId = channelReceiver.GetType().FullName ?? channelReceiver.GetType().Name;
        if (!string.IsNullOrWhiteSpace(subscription?.Name))
        {
            lockId = $"{lockId}:{subscription.Name}";
        }

        var options =
            _configuration.DependencyInjection.GetInstances<SingletonOptions>().LastOrDefault()
            ?? new SingletonOptions();

        var singletonStarter = new SingletonChannelReceiver(
            channelReceiver,
            inFlight,
            lockManager,
            _configuration.Log,
            lockId,
            _teardownToken,
            _placement
        )
        {
            TimerInterval = options.PollInterval,
            PollJitter = options.PollJitter,
            LockDuration = options.LockDuration,
            LockRefreshInterval = options.RenewalInterval,
            Weight = WeightOf(processingSettings, type),
        };
        return singletonStarter;
    }
}
