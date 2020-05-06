using System;
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
        private IQueueClient _client;
        private readonly ManagementClient _managementClient;

        public ServiceBusQueueChannelReceiver(IProcessingSettings settings, IServiceBusConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            _configuration = configuration;
            _hostConfiguration = hostConfiguration;
            _processor = processor;
            Settings = settings;
            _log = hostConfiguration.Log;
            //new client factory per ServiceBusQueueChannelReceiver means a separate communication channel per reader instead of a shared
            _clientFactory = new ClientFactory(configuration.ConnectionString);
            _managementClient = new ManagementClient(configuration.ConnectionString);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client = await _clientFactory.GetQueueClient<T>().ConfigureAwait(false);
            if (!await _managementClient.QueueExistsAsync(_client.QueueName, cancellationToken).ConfigureAwait(false))
            {
                await _managementClient.CreateQueueAsync(new QueueDescription(_client.Path)
                {
                    EnableBatchedOperations = _configuration.CreationOptions.EnableBatchedOperations,
                    EnablePartitioning = _configuration.CreationOptions.EnablePartitioning,
                }, cancellationToken).ConfigureAwait(false);
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

#pragma warning disable 4014
            // ReSharper disable once MethodSupportsCancellation
            Task.Run(async () =>
            {
                cancellationToken.WaitHandle.WaitOne();
                //Cancellation requested
                try
                {
                    _log.Information($"Closing ServiceBus channel receiver for {typeof(T).Name}");
                    await _client.CloseAsync().ConfigureAwait(false);
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
            _log.Information(exceptionReceivedEventArgs.Exception, "MessageHandler received exception");
            return Task.CompletedTask;
        }

        private async Task Handler(Message message, CancellationToken cancellationToken)
        {
            var stateHandler = new ServiceBusMessageStateHandler<T>(_client, message, _configuration.MessageSerializer, _deadLetterLimit, _hostConfiguration.DependencyInjection);
            await _processor.ProcessAsync(stateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}
