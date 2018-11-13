using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;

[assembly: InternalsVisibleTo("BB.Common.KnightBus.Tests.Unit")]
namespace KnightBus.Azure.ServiceBus
{
    internal class ServiceBusQueueTransport<T> : IChannelReceiver
        where T : class, ICommand
    {
        private readonly IClientFactory _clientFactory;
        public IProcessingSettings Settings { get; set; }
        private readonly ILog _log;
        private readonly ServiceBusConfiguration _configuration;
        private readonly IMessageProcessor _processor;
        private int _deadLetterLimit;
        private QueueClient _client;
        private readonly ManagementClient _managementClient;

        public ServiceBusQueueTransport(IProcessingSettings settings, ServiceBusConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            _configuration = configuration;
            _processor = processor;
            Settings = settings;
            _log = hostConfiguration.Log;
            //new client factory per ServiceBusQueueTransport means a separate communication channel per reader instead of a shared
            _clientFactory = new ClientFactory(configuration.ConnectionString);
            _managementClient = new ManagementClient(configuration.ConnectionString);
        }

        public async Task StartAsync()
        {
            _client = await _clientFactory.GetQueueClient<T>().ConfigureAwait(false);
            if (!await _managementClient.QueueExistsAsync(_client.QueueName).ConfigureAwait(false))
            {
                await _managementClient.CreateQueueAsync(new QueueDescription(_client.Path)
                {
                    EnableBatchedOperations = _configuration.CreationOptions.EnableBatchedOperations,
                    EnablePartitioning = _configuration.CreationOptions.EnablePartitioning,
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
            _client.RegisterMessageHandler(Handler, options);
        }

        private Task OnExceptionReceivedAsync(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            _log.Information(exceptionReceivedEventArgs.Exception, "MessageHandler received exception");
            return Task.CompletedTask;
        }

        private async Task Handler(Message message, CancellationToken cancellationToken)
        {
            var stateHandler = new ServiceBusMessageStateHandler<T>(_client, message, _configuration.MessageSerializer, _deadLetterLimit);
            await _processor.ProcessAsync(stateHandler, cancellationToken);
        }
    }
}
