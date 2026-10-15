using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;

namespace KnightBus.Azure.ServiceBus;

internal abstract class ServiceBusChannelReceiverBase<T> : IChannelReceiver
    where T : class, IMessage
{
    private readonly IServiceBusConfiguration _configuration;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageProcessor _processor;
    private readonly IMessageSerializer _serializer;
    protected readonly ILogger Log;
    private int _deadLetterLimit;

    protected readonly IClientFactory ClientFactory;
    protected readonly ServiceBusAdministrationClient ManagementClient;

    protected ServiceBusChannelReceiverBase(
        IProcessingSettings settings,
        IMessageSerializer serializer,
        IServiceBusConfiguration configuration,
        IHostConfiguration hostConfiguration,
        IMessageProcessor processor
    )
    {
        _serializer = serializer;
        _configuration = configuration;
        _hostConfiguration = hostConfiguration;
        _processor = processor;
        Settings = settings;
        Log = hostConfiguration.Log;
        ClientFactory = _hostConfiguration.DependencyInjection.GetInstance<IClientFactory>();
        ManagementClient = ServiceBusClientFactory.CreateServiceBusAdministrationClient(
            configuration
        );
    }

    public IProcessingSettings Settings { get; set; }

    public async Task StartAsync(
        CancellationToken stopFetching,
        CancellationToken cancellationToken
    )
    {
        _deadLetterLimit = Settings.DeadLetterDeliveryLimit;

        //The receiver is started again whenever its singleton lock comes back, so the callbacks of
        //one start only ever use that start's client and token, never those of a later one
        var client = await CreateClient(cancellationToken).ConfigureAwait(false);
        client.ProcessMessageAsync += arg => ClientOnProcessMessageAsync(arg, cancellationToken);
        client.ProcessErrorAsync += arg => ClientOnProcessErrorAsync(arg, cancellationToken);
        await client.StartProcessingAsync(cancellationToken).ConfigureAwait(false);

        //Stopping the processor ends receiving and lets the running handlers finish
        stopFetching.Register(() => _ = StopProcessingAsync(client));
        cancellationToken.Register(() => _ = CloseAsync(client));
    }

    private async Task StopProcessingAsync(ServiceBusProcessor client)
    {
        try
        {
            await client.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.LogWarning(e, "Failed to stop fetching from {MessageType}", typeof(T).Name);
        }
    }

    private async Task CloseAsync(ServiceBusProcessor client)
    {
        try
        {
            Log.LogInformation($"Closing ServiceBus channel receiver for {typeof(T).Name}");
            await client.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            //Swallow
        }
    }

    protected abstract Task<ServiceBusProcessor> CreateClient(CancellationToken cancellationToken);
    protected abstract Task CreateMessagingEntity(CancellationToken cancellationToken);

    private async Task ClientOnProcessErrorAsync(
        ProcessErrorEventArgs arg,
        CancellationToken cancellationToken
    )
    {
        if (
            arg.Exception is ServiceBusException
            {
                Reason: ServiceBusFailureReason.MessagingEntityNotFound
            }
        )
        {
            Log.LogInformation($"{typeof(T).Name} not found. Creating.");
            await CreateMessagingEntity(cancellationToken).ConfigureAwait(false);
        }
        else if (!(arg.Exception is OperationCanceledException))
            Log.LogError(arg.Exception, $"{typeof(T).Name}");
    }

    internal async Task ClientOnProcessMessageAsync(
        ProcessMessageEventArgs arg,
        CancellationToken cancellationToken
    )
    {
        //Not arg.CancellationToken: the SDK cancels it when processing stops, which is also how a
        //drain stops fetching, and a drain must let the message finish
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task OnLockLost(MessageLockLostEventArgs _)
        {
            cts.Cancel();
            return Task.CompletedTask;
        }
        //Listen before anything else: the SDK raises the event once and does not repeat it for a
        //handler that subscribes later. A lock that ran out before the message got here, for
        //example while it waited in the prefetch buffer, raises nothing at all
        arg.MessageLockLostAsync += OnLockLost;
        try
        {
            if (arg.Message.LockedUntil <= DateTimeOffset.UtcNow)
                cts.Cancel();
            var stateHandler = new ServiceBusMessageStateHandler<T>(
                arg,
                _serializer,
                _deadLetterLimit,
                _hostConfiguration.DependencyInjection
            );
            await _processor.ProcessAsync(stateHandler, cts.Token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.LogError(e, "ServiceBus OnMessage Failed");
        }
        finally
        {
            arg.MessageLockLostAsync -= OnLockLost;
        }
    }

    protected IServiceBusCreationOptions GetServiceBusCreationOptions()
    {
        var queueMapping = AutoMessageMapper.GetMapping<T>();
        // ReSharper disable once SuspiciousTypeConversion.Global
        var creationOptions = queueMapping as IServiceBusCreationOptions;

        return creationOptions ?? _configuration.DefaultCreationOptions;
    }
}
