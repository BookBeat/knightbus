using System;
using System.Linq;
using System.Runtime.CompilerServices;
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
        internal readonly SemaphoreSlim _maxConcurrent;
        private Task _runningTask;

        public StorageQueueMessagePump(IStorageQueueClient storageQueueClient, IProcessingSettings settings, ILog log)
        {
            _storageQueueClient = storageQueueClient;
            _settings = settings;
            _log = log;
            _maxConcurrent = new SemaphoreSlim(_settings.MaxConcurrentCalls);
        }

        public Task StartAsync<T>(Func<StorageQueueMessage, CancellationToken, Task> action, CancellationToken cancellationToken) where T : IStorageQueueCommand
        {
            _runningTask = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await PumpAsync<T>(action).ConfigureAwait(false);
                }
            }, cancellationToken);
            return Task.CompletedTask;
        }

        internal async Task PumpAsync<T>(Func<StorageQueueMessage, CancellationToken, Task> action) where T : IStorageQueueCommand
        {
            var messagesFound = false;
            try
            {
                var queueName = AutoMessageMapper.GetQueueName<T>();

                var prefetchCount = _settings.PrefetchCount > 0 ? _settings.PrefetchCount : 1;

                TimeSpan visibilityTimeout;
                if (_settings is IExtendMessageLockTimeout extendMessageLockTimeout)
                {
                    visibilityTimeout = extendMessageLockTimeout.ExtensionDuration;
                }
                else
                {
                    visibilityTimeout = _settings.MessageLockTimeout;
                }

                //Make sure the lock still exist when the process is cancelled by token, otherwise the message cannot be abandoned
                visibilityTimeout += TimeSpan.FromMinutes(2);

                var messages = await _storageQueueClient.GetMessagesAsync<T>(prefetchCount, visibilityTimeout).ConfigureAwait(false);
                messagesFound = messages.Any();

                _log.Debug("Prefetched {MessageCount} messages from {QueueName} in {Name}", messages.Count, queueName, nameof(StorageQueueMessagePump));

                foreach (var message in messages)
                {
                    var cts = new CancellationTokenSource(_settings.MessageLockTimeout);
                    try
                    {
                        _log.Debug("Processing {@Message} in {Name}", message, nameof(StorageQueueMessagePump));
                        _log.Debug("{ThreadCount} remaining threads that can process messages in {QueueName} in {Name}", _maxConcurrent.CurrentCount, queueName, nameof(StorageQueueMessagePump));

                        await _maxConcurrent.WaitAsync(cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException operationCanceledException)
                    {
                        _log.Debug(operationCanceledException, "Operation canceled for {@Message} in {QueueName} in {Name}", message, queueName, nameof(StorageQueueMessagePump));

                        //If we are still waiting when the message has not been scheduled for execution timeouts

                        continue;
                    }
#pragma warning disable 4014 //No need to await the result, let's keep the pump going
                    Task.Run(async () => await action.Invoke(message, cts.Token).ConfigureAwait(false), cts.Token).ContinueWith(task => _maxConcurrent.Release()).ConfigureAwait(false);
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