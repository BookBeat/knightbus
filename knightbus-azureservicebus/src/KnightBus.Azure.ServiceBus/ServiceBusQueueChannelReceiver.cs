using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Messages;

[assembly: InternalsVisibleTo("BB.Common.KnightBus.Tests.Unit")]
namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusQueueChannelReceiver<T> : IChannelReceiver
        where T : class, ICommand
    {
        private readonly IClientFactory _clientFactory;
        public IProcessingSettings Settings { get; set; }
        private readonly ILog _log;
        private readonly IServiceBusConfiguration _configuration;
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageProcessor _processor;
        private int _deadLetterLimit;
        private ServiceBusProcessor _client;
        private readonly ServiceBusAdministrationClient _managementClient;
        private CancellationToken _cancellationToken;

        public ServiceBusQueueChannelReceiver(IProcessingSettings settings, IServiceBusConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
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
            
            if (!await _managementClient.QueueExistsAsync(queueName, _cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var serviceBusCreationOptions = GetServiceBusCreationOptions();

                    await _managementClient.CreateQueueAsync(new CreateQueueOptions(queueName)
                    {
                        EnablePartitioning = serviceBusCreationOptions.EnablePartitioning,
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations,
                        RequiresSession = serviceBusCreationOptions.SupportOrdering
                        
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
            });
#pragma warning restore 4014
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
            var stateHandler = new ServiceBusMessageStateHandler<T>(arg, _configuration.MessageSerializer, _deadLetterLimit, _hostConfiguration.DependencyInjection);
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, arg.CancellationToken))
            {
                await _processor.ProcessAsync(stateHandler, cts.Token).ConfigureAwait(false);
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
