using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;

namespace KnightBus.Core;

/// <summary>
/// Generic implementation of a MessagePump that supports all of the standard KnightBus functionality.
/// </summary>
/// <typeparam name="TInternalRepresentation">The transports internal representation of a message</typeparam>
/// <typeparam name="TMessageInterface">The interface required by the KnightBus transport implementation that implements <see cref="IMessage"/></typeparam>
public abstract class GenericMessagePump<TInternalRepresentation, TMessageInterface> where TMessageInterface : IMessage
{
    private readonly IProcessingSettings _settings;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _maxConcurrent;
    private Task _runningTask;
    private CancellationTokenSource _pumpDelayCancellationTokenSource = new();

    protected GenericMessagePump(IProcessingSettings settings, ILogger log)
    {
        _settings = settings;
        _log = log;
        _maxConcurrent = new SemaphoreSlim(_settings.MaxConcurrentCalls);
    }

    /// <summary>
    /// Starts the message pump
    /// </summary>
    /// <param name="action">The action to be executed for each message, typically the entry into the middleware pipeline</param>
    /// <param name="cancellationToken">CancellationToken, usually the process main shutdown token</param>
    /// <typeparam name="TMessage">The specific message</typeparam>
    /// <returns>A completed Task when the pump has started</returns>
    public virtual Task StartAsync<TMessage>(Func<TInternalRepresentation, CancellationToken, Task> action, CancellationToken cancellationToken) where TMessage : TMessageInterface
    {
        _runningTask = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await PumpAsync<TMessage>(action, cancellationToken).ConfigureAwait(false))
                    await DelayPolling().ConfigureAwait(false);
            }
        }, CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task<bool> PumpAsync<TMessage>(Func<TInternalRepresentation, CancellationToken, Task> action, CancellationToken cancellationToken)
        where TMessage : TMessageInterface
    {
        var messageCount = 0;
        try
        {
            //Do not fetch and lock messages if we won't be able to process them
            if (_maxConcurrent.CurrentCount == 0) return false;

            var queueName = AutoMessageMapper.GetQueueName<TMessage>();

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

            var messages = GetMessagesAsync<TMessage>(prefetchCount, visibilityTimeout);
            var timeoutToken = new CancellationTokenSource(_settings.MessageLockTimeout);
            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken.Token);

            await foreach (var message in messages.ConfigureAwait(false))
            {
                if (message == null) continue;
                messageCount++;
                await _maxConcurrent.WaitAsync(linkedToken.Token).ConfigureAwait(false);

#pragma warning disable 4014 //No need to await the result, let's keep the pump going
                Task.Run(async () => await action.Invoke(message, linkedToken.Token).ConfigureAwait(false), timeoutToken.Token)
                    .ContinueWith(_ =>
                    {
                        _maxConcurrent.Release();
                        timeoutToken.Dispose();
                        linkedToken.Dispose();
                    }, CancellationToken.None).ConfigureAwait(false);
#pragma warning restore 4014
                _log.LogDebug("Prefetched {MessageCount} messages from {QueueName} in {Name}", messageCount, queueName,
                    nameof(GenericMessagePump<TInternalRepresentation, TMessageInterface>));
            }
        }
        catch (Exception e)
        {
            if (ShouldCreateChannel(e))
            {
                _log.LogInformation("{MessageType} not found. Creating.", typeof(TMessage).Name);
                await CreateChannel(typeof(TMessage));
                return false;
            }

            _log.LogError(e, "GenericMessagePump error in {MessageType}", typeof(TMessage));
        }

        return messageCount > 0;
    }

    /// <summary>
    /// Retrieves the messages from the transport
    /// </summary>
    /// <param name="count">Number of messages to receive</param>
    /// <param name="lockDuration">Duration to lock message for other consumers</param>
    /// <typeparam name="TMessage">The type of message</typeparam>
    /// <returns></returns>
    protected abstract IAsyncEnumerable<TInternalRepresentation> GetMessagesAsync<TMessage>(int count, TimeSpan? lockDuration) where TMessage : TMessageInterface;

    /// <summary>
    /// Creates the channel for the specific message if indicated by <see cref="ShouldCreateChannel"/>
    /// </summary>
    /// <param name="messageType">Type of message</param>
    protected abstract Task CreateChannel(Type messageType);
    /// <summary>
    /// Determines if the exception indicates that a new channel should be created
    /// </summary>
    protected abstract bool ShouldCreateChannel(Exception e);

    /// <summary>
    /// Cleanup any expensive resources when shutting down
    /// </summary>
    protected abstract Task CleanupResources();

    /// <summary>
    /// How long should the pump wait if no messages were found before trying again
    /// </summary>
    protected abstract TimeSpan PollingDelay { get; }

    public virtual int AvailableThreads => _maxConcurrent.CurrentCount;

    /// <summary>
    /// Cancels the delay and starts polling for new messages
    /// </summary>
    protected virtual void CancelPollingDelay()
    {
        _pumpDelayCancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Delays the polling for new messages
    /// </summary>
    protected virtual async Task DelayPolling()
    {
        try
        {
            await Task.Delay(PollingDelay, _pumpDelayCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            //reset the delay
            _pumpDelayCancellationTokenSource = new CancellationTokenSource();
        }
    }
}
