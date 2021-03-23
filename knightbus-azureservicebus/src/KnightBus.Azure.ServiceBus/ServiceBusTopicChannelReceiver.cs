using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusTopicChannelReceiver<TTopic> : IChannelReceiver
        where TTopic : class, IEvent
    {
        private readonly IClientFactory _clientFactory;
        public IProcessingSettings Settings { get; set; }
        private readonly ManagementClient _managementClient;
        private readonly IEventSubscription<TTopic> _subscription;
        private readonly ILog _log;
        private readonly IServiceBusConfiguration _configuration;
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageProcessor _processor;
        private int _deadLetterLimit;
        private ServiceBusReceiver _client;
        private StoppableMessageReceiver _messageReceiver;
        private CancellationToken _cancellationToken;

        public ServiceBusTopicChannelReceiver(IProcessingSettings settings, IEventSubscription<TTopic> subscription, IServiceBusConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            Settings = settings;
            _managementClient = new ManagementClient(configuration.ConnectionString);
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
            _client = await _clientFactory.GetReceiverClient<TTopic, IEventSubscription<TTopic>>(_subscription).ConfigureAwait(false);

            if (!await _managementClient.TopicExistsAsync(_client.TopicPath, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var serviceBusCreationOptions = GetServiceBusCreationOptions();

                    await _managementClient.CreateTopicAsync(new TopicDescription(_client.TopicPath)
                    {
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations,
                        EnablePartitioning = serviceBusCreationOptions.EnablePartitioning,
                        SupportOrdering = serviceBusCreationOptions.SupportOrdering
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException e)
                {
                    _log.Error(e, "Failed to create topic {TopicName}", _client.TopicPath);
                    throw;
                }
            }
            if (!await _managementClient.SubscriptionExistsAsync(_client.TopicPath, _client.SubscriptionName, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    var serviceBusCreationOptions = GetServiceBusCreationOptions();

                    await _managementClient.CreateSubscriptionAsync(new SubscriptionDescription(_client.TopicPath, _client.SubscriptionName)
                    {
                        EnableBatchedOperations = serviceBusCreationOptions.EnableBatchedOperations,
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (ServiceBusException e)
                {
                    _log.Error(e, "Failed to create subscription {TopicName} {SubscriptionName}", _client.TopicPath, _client.SubscriptionName);
                    throw;
                }
            }

            _deadLetterLimit = Settings.DeadLetterDeliveryLimit;
            _client.PrefetchCount = Settings.PrefetchCount;
            
            

            _messageReceiver = new StoppableMessageReceiver(_client.ServiceBusConnection, EntityNameHelper.FormatSubscriptionPath(_client.TopicPath, _client.SubscriptionName), ReceiveMode.PeekLock, RetryPolicy.Default, Settings.PrefetchCount);

            var options = new StoppableMessageReceiver.MessageHandlerOptions(OnExceptionReceivedAsync)
            {
                AutoComplete = false,
                MaxAutoRenewDuration = Settings.MessageLockTimeout,
                MaxConcurrentCalls = Settings.MaxConcurrentCalls
            };
            _messageReceiver.RegisterStoppableMessageHandler(options, OnMessageAsync);

#pragma warning disable 4014
            // ReSharper disable once MethodSupportsCancellation
            Task.Run(() =>
            {
                _cancellationToken.WaitHandle.WaitOne();
                //Cancellation requested
                try
                {
                    _log.Information($"Closing ServiceBus channel receiver for {typeof(TTopic).Name}");
                     _messageReceiver.StopPump();
                }
                catch (Exception)
                {
                    //Swallow
                }
            });
#pragma warning restore 4014
        }

        private Task OnExceptionReceivedAsync(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            if (!(exceptionReceivedEventArgs.Exception is OperationCanceledException))
            {
                _log.Error(exceptionReceivedEventArgs.Exception, $"{typeof(ServiceBusTopicChannelReceiver<TTopic>).Name}");
            }
            return Task.CompletedTask;
        }

        private IServiceBusCreationOptions GetServiceBusCreationOptions()
        {
            var queueMapping = AutoMessageMapper.GetMapping<TTopic>();
            var creationOptions = queueMapping as IServiceBusCreationOptions;

            return creationOptions ?? _configuration.DefaultCreationOptions;
        }

        private async Task OnMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
        {
            var stateHandler = new ServiceBusMessageStateHandler<TTopic>(_messageReceiver, message, _configuration.MessageSerializer, _deadLetterLimit, _hostConfiguration.DependencyInjection);
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken))
            {
                await _processor.ProcessAsync(stateHandler, cts.Token).ConfigureAwait(false);
            }
        }
    }
}
