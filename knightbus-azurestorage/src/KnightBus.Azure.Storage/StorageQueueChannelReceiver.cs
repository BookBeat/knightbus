using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Messages;

[assembly: InternalsVisibleTo("KnightBus.Azure.Storage.Tests.Unit")]
[assembly: InternalsVisibleTo("KnightBus.Azure.Storage.Tests.Integration")]
namespace KnightBus.Azure.Storage
{
    internal class StorageQueueChannelReceiver<T> : IChannelReceiver
        where T : class, IStorageQueueCommand
    {
        private IStorageQueueClient _storageQueueClient;
        private readonly IMessageSerializer _serializer;
        private readonly IStorageBusConfiguration _storageOptions;
        private readonly IMessageProcessor _processor;
        private readonly IHostConfiguration _hostConfiguration;
        public IProcessingSettings Settings { get; set; }
        private StorageQueueMessagePump _messagePump;


        public StorageQueueChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IMessageProcessor processor, IHostConfiguration hostConfiguration, IStorageBusConfiguration storageOptions)
        {
            if (settings.PrefetchCount > 32)
                throw new ArgumentOutOfRangeException(nameof(settings),
                    "PrefetchCount is set too high. The maximum number of messages that may be retrieved at a time is 32.");

            Settings = settings;
            _serializer = serializer;
            _storageOptions = storageOptions;
            _processor = processor;
            _hostConfiguration = hostConfiguration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Initialize();
            await _messagePump.StartAsync<T>(Handle, cancellationToken).ConfigureAwait(false);
        }

        private void Initialize()
        {
            var queueName = AutoMessageMapper.GetQueueName<T>();
            _storageQueueClient = new StorageQueueClient(_storageOptions, _serializer, null, queueName);
            _messagePump = new StorageQueueMessagePump(_storageQueueClient, Settings, _hostConfiguration.Log);
        }

        private async Task Handle(StorageQueueMessage message, CancellationToken cancellationToken)
        {
            await _processor.ProcessAsync(new StorageQueueMessageStateHandler<T>(_storageQueueClient, message, Settings.DeadLetterDeliveryLimit, _hostConfiguration.DependencyInjection), cancellationToken).ConfigureAwait(false);
        }
    }
}