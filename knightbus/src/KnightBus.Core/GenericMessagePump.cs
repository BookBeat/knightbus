using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private string _queueName;

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
        _queueName = AutoMessageMapper.GetQueueName<TMessage>();
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

            try
            {
                //Do not fetch and lock messages if we won't be able to process them
                await _maxConcurrent.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _maxConcurrent.Release();
            }

            //Fetch enough messages to fill all AvailableThreads and add Prefetch on top of that as a buffer
            var fetchCount = _settings.PrefetchCount + AvailableThreads;
            fetchCount = Math.Min(fetchCount, MaxFetch);

            TimeSpan visibilityTimeout;
            if (_settings is IExtendMessageLockTimeout extendMessageLockTimeout)
            {
                visibilityTimeout = extendMessageLockTimeout.ExtensionDuration;
            }
            else
            {
                visibilityTimeout = _settings.MessageLockTimeout;
            }

            var stopWatch = Stopwatch.StartNew();
            var messages = GetMessagesAsync<TMessage>(fetchCount, visibilityTimeout);


            await foreach (var message in messages.ConfigureAwait(false))
            {
                if (message == null) continue;
                var remainingLockDuration = _settings.MessageLockTimeout - stopWatch.Elapsed;
                if (remainingLockDuration <= TimeSpan.Zero)
                {
                    // We've waited for longer than the lock duration so exit and resume immediate polling to get messages with renewed locks
                    return true;
                }

                var timeoutToken = new CancellationTokenSource(remainingLockDuration);
                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken.Token);
                messageCount++;
                await _maxConcurrent.WaitAsync(linkedToken.Token).ConfigureAwait(false);

#pragma warning disable 4014 //No need to await the result, let's keep the pump going
                Task.Run(async () =>
                    {
                        try
                        {
                            await action.Invoke(message, linkedToken.Token).ConfigureAwait(false);
                        }
                        finally
                        {
                            _maxConcurrent.Release();
                            timeoutToken.Dispose();
                            linkedToken.Dispose();
                        }
                    }, timeoutToken.Token).ConfigureAwait(false);
#pragma warning restore 4014
            }
            _log.LogDebug("Prefetched {MessageCount} messages from {QueueName} in {Name}", messageCount, _queueName,
                nameof(GenericMessagePump<TInternalRepresentation, TMessageInterface>));
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

    /// <summary>
    /// The maximum number of messages to get at one time from the transport channel.
    /// If there are no limitations on the transport set <see cref="int.MaxValue"/>
    /// </summary>
    protected abstract int MaxFetch { get; }

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
