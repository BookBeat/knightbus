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
    private CancellationToken _cancellationToken;
    private ServiceBusProcessor _client;
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
        ManagementClient = configuration.CreateServiceBusAdministrationClient();
    }

    public IProcessingSettings Settings { get; set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _deadLetterLimit = Settings.DeadLetterDeliveryLimit;

        await InitializeAsync().ConfigureAwait(false);

#pragma warning disable 4014
        Task.Run(
            async () =>
            {
                _cancellationToken.WaitHandle.WaitOne();
                try
                {
                    Log.LogInformation($"Closing ServiceBus channel receiver for {typeof(T).Name}");
                    await _client.CloseAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    //Swallow
                }
            },
            _cancellationToken
        );
#pragma warning restore 4014
    }

    protected abstract Task<ServiceBusProcessor> CreateClient(CancellationToken cancellationToken);
    protected abstract Task CreateMessagingEntity(CancellationToken cancellationToken);

    private async Task InitializeAsync()
    {
        _client = await CreateClient(_cancellationToken).ConfigureAwait(false);
        _client.ProcessMessageAsync += ClientOnProcessMessageAsync;
        _client.ProcessErrorAsync += ClientOnProcessErrorAsync;
        await _client.StartProcessingAsync(_cancellationToken).ConfigureAwait(false);
    }

    private async Task ClientOnProcessErrorAsync(ProcessErrorEventArgs arg)
    {
        if (
            arg.Exception is ServiceBusException
            {
                Reason: ServiceBusFailureReason.MessagingEntityNotFound
            }
        )
        {
            Log.LogInformation($"{typeof(T).Name} not found. Creating.");
            await CreateMessagingEntity(_cancellationToken).ConfigureAwait(false);
        }
        else if (!(arg.Exception is OperationCanceledException))
            Log.LogError(arg.Exception, $"{typeof(T).Name}");
    }

    private async Task ClientOnProcessMessageAsync(ProcessMessageEventArgs arg)
    {
        try
        {
            var stateHandler = new ServiceBusMessageStateHandler<T>(
                arg,
                _serializer,
                _deadLetterLimit,
                _hostConfiguration.DependencyInjection
            );
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationToken,
                arg.CancellationToken
            );
            await _processor.ProcessAsync(stateHandler, cts.Token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log.LogError(e, "ServiceBus OnMessage Failed");
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
