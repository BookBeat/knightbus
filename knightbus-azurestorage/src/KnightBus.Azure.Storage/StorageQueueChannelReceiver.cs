using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;

[assembly: InternalsVisibleTo("KnightBus.Azure.Storage.Tests.Unit")]
namespace KnightBus.Azure.Storage
{
    internal class StorageQueueChannelReceiver<T> : IChannelReceiver
        where T : class, IStorageQueueCommand
    {
        private IStorageQueueClient _storageQueueClient;
        private readonly IStorageBusConfiguration _storageOptions;
        private readonly IMessageProcessor _processor;
        private readonly IHostConfiguration _hostConfiguration;
        public IProcessingSettings Settings { get; set; }
        private StorageQueueMessagePump _messagePump;


        public StorageQueueChannelReceiver(IProcessingSettings settings, IMessageProcessor processor, IHostConfiguration hostConfiguration, IStorageBusConfiguration storageOptions)
        {
            if (settings.PrefetchCount > 32)
                throw new ArgumentOutOfRangeException(nameof(settings),
                    "PrefetchCount is set too high. The maximum number of messages that may be retrieved at a time is 32.");

            Settings = settings;
            _storageOptions = storageOptions;
            _processor = processor;
            _hostConfiguration = hostConfiguration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Initialize().ConfigureAwait(false);
            await _messagePump.StartAsync<T>(Handle, cancellationToken).ConfigureAwait(false);
        }

        private async Task Initialize()
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            _storageQueueClient = new StorageQueueClient(_storageOptions, null, queueName);
            await _storageQueueClient.CreateIfNotExistsAsync().ConfigureAwait(false);
            _messagePump = new StorageQueueMessagePump(_storageQueueClient, Settings, _hostConfiguration.Log);
        }

        private async Task Handle(StorageQueueMessage message, CancellationToken cancellationToken)
        {
            await _processor.ProcessAsync(new StorageQueueMessageStateHandler<T>(_storageQueueClient, message, Settings.DeadLetterDeliveryLimit, _hostConfiguration.DependencyInjection), cancellationToken).ConfigureAwait(false);
        }
    }
}