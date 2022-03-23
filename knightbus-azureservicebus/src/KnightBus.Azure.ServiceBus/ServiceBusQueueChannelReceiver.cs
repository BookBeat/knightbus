using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core;
using KnightBus.Messages;

[assembly: InternalsVisibleTo("BB.Common.KnightBus.Tests.Unit")]
namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusQueueChannelReceiver<T> : IChannelReceiver
        where T : class, ICommand
    {
        private IClientFactory _clientFactory;
        public IProcessingSettings Settings { get; set; }
        private readonly ILog _log;
        private readonly IMessageSerializer _serializer;
        private readonly IServiceBusConfiguration _configuration;
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageProcessor _processor;
        private int _deadLetterLimit;
        private ServiceBusProcessor _client;
        private ServiceBusAdministrationClient _managementClient;
        private CancellationToken _cancellationToken;
        private CancellationTokenSource _shutdownTaskCancellation;
        private DateTimeOffset? _lastProcess;
        private AutoResetEvent _throughputAutoResetEvent;
        private Timer _throughputTimer;

        public ServiceBusQueueChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IServiceBusConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            _serializer = serializer;
            _configuration = configuration;
            _hostConfiguration = hostConfiguration;
            _processor = processor;
            Settings = settings;
            _log = hostConfiguration.Log;
            //new client factory per ServiceBusQueueChannelReceiver means a separate communication channel per reader instead of a shared
            _clientFactory = new ClientFactory(configuration.ConnectionString);
            _managementClient = new ServiceBusAdministrationClient(configuration.ConnectionString);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _client = await _clientFactory.GetReceiverClient<T>(new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = Settings.MessageLockTimeout,
                MaxConcurrentCalls = Settings.MaxConcurrentCalls,
                PrefetchCount = Settings.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock

            }).ConfigureAwait(false);

            var queueName = AutoMessageMapper.GetQueueName<T>();

            //TODO: optimistic queue check
            if (!await _managementClient.QueueExistsAsync(queueName, _cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var serviceBusCreationOptions = GetServiceBusCreationOptions();

                    await _managementClient.CreateQueueAsync(new CreateQueueOptions(queueName)
                    {
                        EnablePartitioning = serviceBusCreationOptions.EnablePartitioning,
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations

                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException e)
                {
                    _log.Error(e, "Failed to create queue {QueueName}", queueName);
                    throw;
                }
            }
            _deadLetterLimit = Settings.DeadLetterDeliveryLimit;

            _client.ProcessMessageAsync += ClientOnProcessMessageAsync;
            _client.ProcessErrorAsync += ClientOnProcessErrorAsync;

            await _client.StartProcessingAsync(cancellationToken);

#pragma warning disable 4014
            // ReSharper disable once MethodSupportsCancellation
            _shutdownTaskCancellation = new CancellationTokenSource();

            Task.Run(async () =>
            {
                _cancellationToken.WaitHandle.WaitOne();
                //Cancellation requested
                try
                {
                    _log.Information($"Closing ServiceBus channel receiver for {typeof(T).Name}");
                    await _client.CloseAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    //Swallow
                }
            }, _shutdownTaskCancellation.Token);
#pragma warning restore 4014

            _throughputAutoResetEvent = new AutoResetEvent(false);
            _throughputTimer = new Timer(CheckThroughput, _throughputAutoResetEvent, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)); // TODO: make interval configurable?
        }

        private void CheckThroughput(object state)
        {
            var timeSinceLastProcessedMessage = DateTimeOffset.UtcNow - _lastProcess;
            if (timeSinceLastProcessedMessage == null || timeSinceLastProcessedMessage < TimeSpan.FromSeconds(30)) return; // TODO: make allowed idle time configurable?

            _log.Information("Last message was processed {MinutesSinceLastProcessedMessage} minutes ago, restarting", timeSinceLastProcessedMessage?.TotalMinutes);
            _ = RestartConnection();
        }

        private async Task RestartConnection()
        {
            try
            {
                _lastProcess = null;

                await _client.StopProcessingAsync(_cancellationToken);
                _client.ProcessMessageAsync -= ClientOnProcessMessageAsync;
                _client.ProcessErrorAsync -= ClientOnProcessErrorAsync;

                _shutdownTaskCancellation.Cancel();

                _throughputAutoResetEvent.Dispose();
                await _throughputTimer.DisposeAsync();
                await _clientFactory.DisposeAsync();

                // TODO: do we want to new up new instances or something else?
                _clientFactory = new ClientFactory(_configuration.ConnectionString);
                _managementClient = new ServiceBusAdministrationClient(_configuration.ConnectionString);

                await StartAsync(_cancellationToken);
            }
            catch (Exception)
            {
                // TODO: what to do here?
                throw;
            }
        }

        private Task ClientOnProcessErrorAsync(ProcessErrorEventArgs arg)
        {
            if (!(arg.Exception is OperationCanceledException))
            {
                _log.Error(arg.Exception, $"{typeof(ServiceBusQueueChannelReceiver<T>).Name}");
            }
            return Task.CompletedTask;
        }

        private async Task ClientOnProcessMessageAsync(ProcessMessageEventArgs arg)
        {
            try
            {
                var stateHandler = new ServiceBusMessageStateHandler<T>(arg, _serializer, _deadLetterLimit, _hostConfiguration.DependencyInjection);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, arg.CancellationToken);
                await _processor.ProcessAsync(stateHandler, cts.Token).ConfigureAwait(false);
                _lastProcess = DateTimeOffset.UtcNow;
            }
            catch (Exception e)
            {
                _log.Error(e, "ServiceBus OnMessage Failed");
            }
        }

        private IServiceBusCreationOptions GetServiceBusCreationOptions()
        {
            var queueMapping = AutoMessageMapper.GetMapping<T>();
            var creationOptions = queueMapping as IServiceBusCreationOptions;

            return creationOptions ?? _configuration.DefaultCreationOptions;
        }
    }
}
