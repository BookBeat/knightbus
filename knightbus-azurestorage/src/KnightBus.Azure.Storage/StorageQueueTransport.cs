using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;

[assembly: InternalsVisibleTo("KnightBus.Azure.Storage.Tests.Unit")]
namespace KnightBus.Azure.Storage
{
    internal class StorageQueueTransport<T, TSettings> : IChannelReceiver
        where T : class, IStorageQueueCommand
        where TSettings : class, IProcessingSettings, new()
    {
        private IStorageQueueClient _storageQueueClient;
        private readonly IStorageBusConfiguration _storageOptions;
        private readonly IMessageProcessor _processor;
        private readonly IHostConfiguration _hostConfiguration;
        public IProcessingSettings Settings { get; set; }
        public ITransportConfiguration Configuration => _storageOptions;
        private StorageQueueMessagePump _messagePump;
        

        public StorageQueueTransport(IProcessingSettings settings, IMessageProcessor processor, IHostConfiguration hostConfiguration, IStorageBusConfiguration storageOptions)
        {
            if (settings.PrefetchCount > 32)
                throw new ArgumentOutOfRangeException(nameof(settings),
                    "PrefetchCount is set too high. The maximum number of messages that may be retrieved at a time is 32.");

            Settings = settings;
            _storageOptions = storageOptions;
            _processor = processor;
            _hostConfiguration = hostConfiguration;
        }

        public async Task StartAsync()
        {
            await Initialize().ConfigureAwait(false);
            await _messagePump.StartAsync<T>(Handle).ConfigureAwait(false);
        }

        private async Task Initialize()
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            _storageQueueClient = new StorageQueueClient(_storageOptions, queueName);
            await _storageQueueClient.CreateIfNotExistsAsync().ConfigureAwait(false);
            _messagePump = new StorageQueueMessagePump(_storageQueueClient, Settings, _hostConfiguration.Log);
        }

        private async Task Handle(StorageQueueMessage message, CancellationToken cancellationToken)
        {
            await _processor.ProcessAsync(new StorageQueueMessageStateHandler<T>(_storageQueueClient, message, Settings.DeadLetterDeliveryLimit), cancellationToken).ConfigureAwait(false);
        }
    }
}