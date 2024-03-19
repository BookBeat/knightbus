using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using Microsoft.Extensions.Logging;

namespace KnightBus.Azure.Storage;

internal class StorageQueueMessagePump
{
    private readonly IStorageQueueClient _storageQueueClient;
    private readonly IProcessingSettings _settings;
    private readonly ILogger _log;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMilliseconds(5000);
    internal readonly SemaphoreSlim _maxConcurrent;
    private Task _runningTask;

    public StorageQueueMessagePump(IStorageQueueClient storageQueueClient, IProcessingSettings settings, ILogger log)
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
                await PumpAsync<T>(action, cancellationToken).ConfigureAwait(false);
            }
        });
        return Task.CompletedTask;
    }

    internal async Task PumpAsync<T>(Func<StorageQueueMessage, CancellationToken, Task> action, CancellationToken cancellationToken) where T : IStorageQueueCommand
    {
        var messagesFound = false;
        try
        {
            //Do not fetch and lock messages if we won't be able to process them
            if (_maxConcurrent.CurrentCount == 0) return;

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

            var messages = await _storageQueueClient.GetMessagesAsync<T>(prefetchCount, visibilityTimeout)
                .ConfigureAwait(false);
            messagesFound = messages.Any();

            _log.LogDebug("Prefetched {MessageCount} messages from {QueueName} in {Name}", messages.Count, queueName,
                nameof(StorageQueueMessagePump));

            foreach (var message in messages)
            {
                var timeoutToken = new CancellationTokenSource(_settings.MessageLockTimeout);
                var linkedToken =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken.Token);
                try
                {
                    _log.LogDebug("Processing {@Message} in {Name}", message, nameof(StorageQueueMessagePump));
                    _log.LogDebug("{ThreadCount} remaining threads that can process messages in {QueueName} in {Name}",
                        _maxConcurrent.CurrentCount, queueName, nameof(StorageQueueMessagePump));

                    await _maxConcurrent.WaitAsync(timeoutToken.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException operationCanceledException)
                {
                    _log.LogDebug(operationCanceledException,
                        "Operation canceled for {@Message} in {QueueName} in {Name}", message, queueName,
                        nameof(StorageQueueMessagePump));

                    //If we are still waiting when the message has not been scheduled for execution timeouts

                    continue;
                }
#pragma warning disable 4014 //No need to await the result, let's keep the pump going
                Task.Run(async () => await action.Invoke(message, linkedToken.Token).ConfigureAwait(false),
                        timeoutToken.Token)
                    .ContinueWith(task =>
                    {
                        _maxConcurrent.Release();
                        timeoutToken.Dispose();
                        linkedToken.Dispose();
                    }).ConfigureAwait(false);
#pragma warning restore 4014
            }
        }
        catch (RequestFailedException e) when (e.Status is (int)HttpStatusCode.NotFound)
        {
            _log.LogInformation($"{typeof(T).Name} not found. Creating.");
            await _storageQueueClient.CreateIfNotExistsAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _log.LogError(e, "StorageQueueMessagePump error in {MessageType}", typeof(T));
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
