using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusTopicChannelReceiver<TTopic> : IChannelReceiver
        where TTopic : class, IEvent
    {
        private readonly IClientFactory _clientFactory;
        public IProcessingSettings Settings { get; set; }
        private readonly ServiceBusAdministrationClient _managementClient;
        private readonly IMessageSerializer _serializer;
        private readonly IEventSubscription<TTopic> _subscription;
        private readonly ILog _log;
        private readonly IServiceBusConfiguration _configuration;
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageProcessor _processor;
        private int _deadLetterLimit;
        private ServiceBusProcessor _client;
        private CancellationToken _cancellationToken;

        public ServiceBusTopicChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IEventSubscription<TTopic> subscription, IServiceBusConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            Settings = settings;
            _managementClient = new ServiceBusAdministrationClient(configuration.ConnectionString);
            _serializer = serializer;
            _subscription = subscription;
            _log = hostConfiguration.Log;
            _configuration = configuration;
            _hostConfiguration = hostConfiguration;
            _processor = processor;
            //new client factory per ServiceBusTopicChannelReceiver means a separate communication channel per reader instead of a shared
            _clientFactory = new ClientFactory(configuration.ConnectionString);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _client = await _clientFactory.GetReceiverClient<TTopic, IEventSubscription<TTopic>>(_subscription, new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = Settings.MessageLockTimeout,
                MaxConcurrentCalls = Settings.MaxConcurrentCalls,
                PrefetchCount = Settings.PrefetchCount,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
                
            }).ConfigureAwait(false);

            
            var topicName = AutoMessageMapper.GetQueueName<TTopic>();
            
            if (!await _managementClient.TopicExistsAsync(topicName, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var serviceBusCreationOptions = GetServiceBusCreationOptions();

                    await _managementClient.CreateTopicAsync(new CreateTopicOptions(topicName)
                    {
                        EnablePartitioning = serviceBusCreationOptions.EnablePartitioning,
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations,
                        SupportOrdering = serviceBusCreationOptions.SupportOrdering

                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException e)
                {
                    _log.Error(e, "Failed to create topic {TopicName}", topicName);
                    throw;
                }
            }
            if (!await _managementClient.SubscriptionExistsAsync(topicName, _subscription.Name, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var serviceBusCreationOptions = GetServiceBusCreationOptions();

                    await _managementClient.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, _subscription.Name)
                    {
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException e)
                {
                    _log.Error(e, "Failed to create subscription {TopicName} {SubscriptionName}", topicName, _subscription.Name);
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
                    _log.Information($"Closing ServiceBus channel receiver for {typeof(TTopic).Name}");
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
                _log.Error(arg.Exception, $"{typeof(ServiceBusTopicChannelReceiver<TTopic>).Name}");
            }
            return Task.CompletedTask;
        }

        private async Task ClientOnProcessMessageAsync(ProcessMessageEventArgs arg)
        {
            try
            {
                var stateHandler = new ServiceBusMessageStateHandler<TTopic>(arg, _serializer, _deadLetterLimit, _hostConfiguration.DependencyInjection);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, arg.CancellationToken);
                await _processor.ProcessAsync(stateHandler, cts.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _log.Error(e, "ServiceBus OnMessage Failed");
            }
        }

        private IServiceBusCreationOptions GetServiceBusCreationOptions()
        {
            var queueMapping = AutoMessageMapper.GetMapping<TTopic>();
            var creationOptions = queueMapping as IServiceBusCreationOptions;

            return creationOptions ?? _configuration.DefaultCreationOptions;
        }
    }
}
