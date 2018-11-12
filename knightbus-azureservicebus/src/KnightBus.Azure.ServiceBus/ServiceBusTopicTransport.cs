using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;

namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusTopicTransport<TTopic> : IChannelReceiver
        where TTopic : class, IEvent
    {
        private readonly IClientFactory _clientFactory;
        public IProcessingSettings Settings { get; set; }
        private readonly ManagementClient _managementClient;
        private readonly IEventSubscription<TTopic> _subscription;
        private readonly ILog _log;
        private readonly ServiceBusConfiguration _configuration;
        private readonly IMessageProcessor _processor;
        private int _deadLetterLimit;
        private SubscriptionClient _client;
        

        public ServiceBusTopicTransport(IProcessingSettings settings, IEventSubscription<TTopic> subscription, ServiceBusConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            Settings = settings;
            _managementClient = new ManagementClient(configuration.ConnectionString);
            _subscription = subscription;
            _log = hostConfiguration.Log;
            _configuration = configuration;
            _processor = processor;
            //new client factory per ServiceBusTopicTransport means a separate communication channel per reader instead of a shared
            _clientFactory = new ClientFactory(configuration.ConnectionString);
        }

        public async Task StartAsync()
        {
            _client = await _clientFactory.GetSubscriptionClient<TTopic, IEventSubscription<TTopic>>(_subscription).ConfigureAwait(false);

            if (!await _managementClient.TopicExistsAsync(_client.TopicPath).ConfigureAwait(false))
            {
                await _managementClient.CreateTopicAsync(new TopicDescription(_client.TopicPath)
                {
                    EnableBatchedOperations = _configuration.CreationOptions.EnableBatchedOperations,
                    EnablePartitioning = _configuration.CreationOptions.EnablePartitioning,
                    SupportOrdering = _configuration.CreationOptions.SupportOrdering
                }).ConfigureAwait(false);
            }
            if (!await _managementClient.SubscriptionExistsAsync(_client.TopicPath, _client.SubscriptionName).ConfigureAwait(false))
            {
                await _managementClient.CreateSubscriptionAsync(new SubscriptionDescription(_client.TopicPath, _client.SubscriptionName)
                {
                    EnableBatchedOperations = _configuration.CreationOptions.EnableBatchedOperations
                }).ConfigureAwait(false);
            }

            _deadLetterLimit = Settings.DeadLetterDeliveryLimit;
            _client.PrefetchCount = Settings.PrefetchCount;
            var options = new MessageHandlerOptions(OnExceptionReceivedAsync)
            {
                AutoComplete = false,
                MaxAutoRenewDuration = Settings.MessageLockTimeout,
                MaxConcurrentCalls = Settings.MaxConcurrentCalls
            };
            _client.RegisterMessageHandler(OnMessageAsync, options);
        }

        private Task OnExceptionReceivedAsync(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            _log.Information(exceptionReceivedEventArgs.Exception, "Message Handler received exception");
            return Task.CompletedTask;
        }

        private async Task OnMessageAsync(Message message, CancellationToken cancellationToken)
        {
            var stateHandler = new ServiceBusMessageStateHandler<TTopic>(_client, message, _configuration.MessageSerializer, _deadLetterLimit);
            await _processor.ProcessAsync(stateHandler, cancellationToken);
        }
    }
}