using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;

namespace KnightBus.Azure.Storage
{
    internal class StorageQueueMessagePump
    {
        private readonly IStorageQueueClient _storageQueueClient;
        private readonly IProcessingSettings _settings;
        private readonly ILog _log;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromMilliseconds(5000);
        private readonly SemaphoreSlim _maxConcurrent;
        private Task _runningTask;

        public StorageQueueMessagePump(IStorageQueueClient storageQueueClient, IProcessingSettings settings, ILog log)
        {
            _storageQueueClient = storageQueueClient;
            _settings = settings;
            _log = log;
            _maxConcurrent = new SemaphoreSlim(_settings.MaxConcurrentCalls);
        }

        public Task StartAsync<T>(Func<StorageQueueMessage, CancellationToken, Task> action) where T : IStorageQueueCommand
        {
            _runningTask = Task.Run(async () =>
            {
                while (true)
                {
                    await PumpAsync<T>(action).ConfigureAwait(false);
                }
            });
            return Task.CompletedTask;
        }

        internal async Task PumpAsync<T>(Func<StorageQueueMessage, CancellationToken, Task> action) where T : IStorageQueueCommand
        {
            var messagesFound = false;
            try
            {
                var prefetchCount = _settings.PrefetchCount > 0 ? _settings.PrefetchCount : 1;
                var messages = await _storageQueueClient.GetMessagesAsync<T>(prefetchCount, _settings.MessageLockTimeout).ConfigureAwait(false);
                messagesFound = messages.Any();
                foreach (var message in messages)
                {
                    await _maxConcurrent.WaitAsync().ConfigureAwait(false);
                    var cts = new CancellationTokenSource(_settings.MessageLockTimeout);
#pragma warning disable 4014 //No need to await the result, let's keep the pump going
                    action.Invoke(message, cts.Token).ContinueWith(task => _maxConcurrent.Release());
#pragma warning restore 4014
                }
            }
            catch (Exception e)
            {
                _log.Error(e, "StorageQueueMessagePump error in {MessageType}", typeof(T));
            }
            finally
            {
                if (!messagesFound)
                {
                    //Only delay pump if no messages were found
                    await Task.Delay(_pollingInterval).ConfigureAwait(false);
                }
                
            }
        }
    }
}