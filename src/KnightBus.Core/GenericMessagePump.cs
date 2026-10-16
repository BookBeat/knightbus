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
public abstract class GenericMessagePump<TInternalRepresentation, TMessageInterface>
    where TMessageInterface : IMessage
{
    protected readonly IProcessingSettings Settings;
    protected readonly ILogger Log;
    private readonly SemaphoreSlim _maxConcurrent;
    private Task? _runningTask;
    private CancellationTokenSource _pumpDelayCancellationTokenSource = new();
    private CancellationToken _pumpCancellationToken = CancellationToken.None;
    private string _queueName = null!;

    protected GenericMessagePump(IProcessingSettings settings, ILogger log)
    {
        Settings = settings;
        Log = log;
        _maxConcurrent = new SemaphoreSlim(
            Settings.MaxConcurrentCalls,
            Settings.MaxConcurrentCalls
        );
    }

    /// <summary>
    /// Starts the message pump
    /// </summary>
    /// <param name="action">The action to be executed for each message, typically the entry into the middleware pipeline</param>
    /// <param name="stopFetching">Stops fetching new messages, the ones already being handled carry on</param>
    /// <param name="cancellationToken">Stops the pump and cancels the messages being handled, usually the process main shutdown token</param>
    /// <typeparam name="TMessage">The specific message</typeparam>
    /// <returns>A completed Task when the pump has started</returns>
    /// <remarks><see cref="CleanupResources"/> runs once fetching has stopped</remarks>
    public virtual Task StartAsync<TMessage>(
        Func<TInternalRepresentation, CancellationToken, Task> action,
        CancellationToken stopFetching,
        CancellationToken cancellationToken
    )
        where TMessage : TMessageInterface
    {
        _queueName = AutoMessageMapper.GetQueueName<TMessage>();
        var fetching = CancellationTokenSource.CreateLinkedTokenSource(
            stopFetching,
            cancellationToken
        );
        //The polling delay observes this token so a sleeping pump exits promptly
        _pumpCancellationToken = fetching.Token;
        _runningTask = Task.Run(
            async () =>
            {
                while (!fetching.IsCancellationRequested)
                {
                    try
                    {
                        if (
                            !await PumpAsync<TMessage>(action, fetching.Token, cancellationToken)
                                .ConfigureAwait(false)
                        )
                            await DelayPolling().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        //The pump task is never awaited, so an escaping exception would kill the pump silently
                        Log.LogError(
                            e,
                            "Unhandled error in message pump for {MessageType}",
                            typeof(TMessage)
                        );
                        await DelayPolling().ConfigureAwait(false);
                    }
                }
                fetching.Dispose();

                try
                {
                    await CleanupResources().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    //The pump task is never awaited, so an escaping exception would be unobserved
                    Log.LogError(
                        e,
                        "Failed to cleanup resources in message pump for {MessageType}",
                        typeof(TMessage)
                    );
                }
            },
            CancellationToken.None
        );
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fetches once and hands the messages to <paramref name="action"/>
    /// </summary>
    /// <param name="action">The action to be executed for each message</param>
    /// <param name="stopFetching">Ends the wait for room and stops the fetch. It must also fire when
    /// <paramref name="cancellationToken"/> does, as the one <see cref="StartAsync{TMessage}"/> passes does</param>
    /// <param name="cancellationToken">Cancels the messages being handled</param>
    public async Task<bool> PumpAsync<TMessage>(
        Func<TInternalRepresentation, CancellationToken, Task> action,
        CancellationToken stopFetching,
        CancellationToken cancellationToken
    )
        where TMessage : TMessageInterface
    {
        var messageCount = 0;
        try
        {
            //Do not fetch and lock messages if we won't be able to process them, and do not fetch
            //at all once fetching has stopped, however long the wait for room was
            await _maxConcurrent.WaitAsync(stopFetching).ConfigureAwait(false);
            _maxConcurrent.Release();
            if (stopFetching.IsCancellationRequested)
                return false;

            //Fetch enough messages to fill all AvailableThreads and add Prefetch on top of that as a buffer
            var fetchCount = Settings.PrefetchCount + AvailableThreads;
            fetchCount = Math.Min(fetchCount, MaxFetch);

            var stopWatch = Stopwatch.StartNew();
            var messages = GetMessagesAsync<TMessage>(fetchCount, VisibilityTimeout);

            await foreach (var message in messages.ConfigureAwait(false))
            {
                if (message == null)
                    continue;
                var remainingLockDuration = Settings.MessageLockTimeout - stopWatch.Elapsed;
                if (remainingLockDuration <= TimeSpan.Zero)
                {
                    // We've waited for longer than the lock duration so exit and resume immediate polling to get messages with renewed locks
                    return true;
                }

                if (
                    !await DispatchAsync(message, remainingLockDuration, action, cancellationToken)
                        .ConfigureAwait(false)
                )
                    return true;
                messageCount++;
            }

            Log.LogDebug(
                "Prefetched {MessageCount} messages from {QueueName} in {Name}",
                messageCount,
                _queueName,
                nameof(GenericMessagePump<TInternalRepresentation, TMessageInterface>)
            );
        }
        catch (OperationCanceledException) when (stopFetching.IsCancellationRequested)
        {
            //Fetching stopped while waiting for room, which is not an error
        }
        catch (Exception e)
        {
            if (ShouldCreateChannel(e))
            {
                Log.LogInformation("{MessageType} not found. Creating.", typeof(TMessage).Name);
                try
                {
                    await CreateChannel(typeof(TMessage)).ConfigureAwait(false);
                }
                catch (Exception createException)
                {
                    Log.LogError(
                        createException,
                        "Failed to create channel for {MessageType}",
                        typeof(TMessage)
                    );
                }
                return false;
            }

            Log.LogError(e, "GenericMessagePump error in {MessageType}", typeof(TMessage));
        }

        return messageCount > 0;
    }

    //How long a fetched message stays invisible to other receivers
    private TimeSpan VisibilityTimeout =>
        Settings is IExtendMessageLockTimeout extendMessageLockTimeout
            ? extendMessageLockTimeout.ExtensionDuration
            : Settings.MessageLockTimeout;

    /// <summary>
    /// Hands a fetched message to <paramref name="action"/> once there is room, false when its lock
    /// ran out first
    /// </summary>
    private async Task<bool> DispatchAsync(
        TInternalRepresentation message,
        TimeSpan remainingLockDuration,
        Func<TInternalRepresentation, CancellationToken, Task> action,
        CancellationToken cancellationToken
    )
    {
        var timeoutToken = new CancellationTokenSource(remainingLockDuration);
        var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutToken.Token
        );
        try
        {
            //Not the stop token: a fetched message is handed on even after a stop, with its
            //token cancelled, so the pipeline returns it instead of it staying invisible
            //until its lock runs out
            await _maxConcurrent.WaitAsync(timeoutToken.Token).ConfigureAwait(false);
        }
        catch
        {
            timeoutToken.Dispose();
            linkedToken.Dispose();
            return false;
        }

#pragma warning disable 4014 //No need to await the result, let's keep the pump going
        //No scheduling token here: if the lock timeout fired before the task started,
        //the delegate would be skipped and the finally would never release the slot
        Task.Run(
                async () =>
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
                },
                CancellationToken.None
            )
            .ConfigureAwait(false);
#pragma warning restore 4014
        return true;
    }

    /// <summary>
    /// Retrieves the messages from the transport
    /// </summary>
    /// <param name="count">Number of messages to receive</param>
    /// <param name="lockDuration">Duration to lock message for other consumers</param>
    /// <typeparam name="TMessage">The type of message</typeparam>
    /// <returns></returns>
    protected abstract IAsyncEnumerable<TInternalRepresentation?> GetMessagesAsync<TMessage>(
        int count,
        TimeSpan? lockDuration
    )
        where TMessage : TMessageInterface;

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
    /// Cleanup any expensive resources owned by this pump instance.
    /// Called once when the pump has stopped fetching, which can be while the messages it handed
    /// out are still being handled, so do not release anything they need to complete or abandon.
    /// Do not release resources shared with other components either, since the pump can be stopped
    /// while the rest of the host keeps running.
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
        if (_pumpCancellationToken.IsCancellationRequested)
            return;

        var delayCancellation = _pumpDelayCancellationTokenSource;
        //A scoped linked source that is disposed on every path, a long-lived linked source
        //would pin one registration on the shutdown token per CancelPollingDelay call
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            delayCancellation.Token,
            _pumpCancellationToken
        );
        try
        {
            await Task.Delay(PollingDelay, linked.Token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            if (_pumpCancellationToken.IsCancellationRequested)
                return;
            //Only replace the source we waited on, so a concurrent CancelPollingDelay
            //signal is never silently dropped
            Interlocked.CompareExchange(
                ref _pumpDelayCancellationTokenSource,
                new CancellationTokenSource(),
                delayCancellation
            );
        }
    }
}
