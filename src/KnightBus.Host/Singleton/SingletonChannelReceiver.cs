using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;

namespace KnightBus.Host.Singleton;

internal class SingletonChannelReceiver : IChannelReceiver
{
    private readonly IChannelReceiver _channelReceiver;
    private readonly ISingletonLockManager _lockManager;
    private readonly ILogger _log;
    private readonly CancellationToken? _teardownToken;
    private SingletonTimerScope? _singletonScope;
    private readonly string _lockId;
    internal string LockId => _lockId;
    public IProcessingSettings Settings { get; set; }
    internal TimeSpan TimerInterval { get; set; } = TimeSpan.FromMinutes(1);
    internal TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(1);
    internal TimeSpan LockRefreshInterval { get; set; } = TimeSpan.FromSeconds(19);

    //Written by the lock-lost watcher thread and read by the timer loop
    private volatile bool _lockPollingEnabled = false;

    //Incremented for every successful lock acquisition, so a stale watcher from a previous
    //acquisition cannot re-enable polling after the lock has already been re-acquired
    private int _acquisitionGeneration;
    private Task? _pollingLoop;

    /// <summary>
    /// Completes when the lock held by this receiver has been released
    /// </summary>
    internal Task TeardownCompletion => _singletonScope?.Completion ?? Task.CompletedTask;

    public SingletonChannelReceiver(
        IChannelReceiver channelReceiver,
        ISingletonLockManager lockManager,
        ILogger log,
        string? lockId = null,
        CancellationToken? teardownToken = null
    )
    {
        _channelReceiver = channelReceiver;
        _lockManager = lockManager;
        _log = log;
        _teardownToken = teardownToken;
        _lockId = lockId ?? channelReceiver.GetType().FullName!;
        //MaxConcurrent and Prefetch must have specific  values to work with a singleton implementation.
        //Override those and let the other values be set from the specific implementation
        Settings = new SingletonProcessingSettings
        {
            MessageLockTimeout = _channelReceiver.Settings.MessageLockTimeout,
            DeadLetterDeliveryLimit = _channelReceiver.Settings.DeadLetterDeliveryLimit,
        };
        _channelReceiver.Settings = Settings;
    }

    private async Task TimerLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_lockPollingEnabled)
            {
                await AcquireLock(cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(TimerInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AcquireLock(CancellationToken cancellationToken)
    {
        //Try and get the lock
        var lockHandle = await _lockManager
            .TryLockAsync(_lockId, LockDuration, cancellationToken)
            .ConfigureAwait(false);

        if (lockHandle != null)
        {
            //The lock is held and renewed until the teardown token fires, while the wrapped
            //receiver stops on the ordinary shutdown token. This keeps the lock through the
            //host's message drain, so no other instance can start processing while this one
            //is still finishing in-flight messages. Without a teardown token both phases
            //collapse into one and the lock is released on the shutdown token.
            var scopeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                _teardownToken ?? cancellationToken
            );
            var receiverTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                scopeTokenSource.Token
            );
            var generation = Interlocked.Increment(ref _acquisitionGeneration);
            _singletonScope = new SingletonTimerScope(
                _log,
                lockHandle,
                true,
                LockRefreshInterval,
                scopeTokenSource
            );
            _log.LogInformation("Starting Singleton Processor with name {ProcessorName}", _lockId);
            await _channelReceiver.StartAsync(receiverTokenSource.Token).ConfigureAwait(false);
            _lockPollingEnabled = false;

#pragma warning disable 4014
            Task.Run(
                    () =>
                    {
                        scopeTokenSource.Token.WaitHandle.WaitOne();
                        //Stop signal received, restart the polling. A watcher that wakes late,
                        //after the lock has already been re-acquired, must not restart it
                        if (
                            !cancellationToken.IsCancellationRequested
                            && Volatile.Read(ref _acquisitionGeneration) == generation
                        )
                        {
                            _lockPollingEnabled = true;
                            _log.LogInformation(
                                "Singleton Processor with name {ProcessorName} lost its lock",
                                _lockId
                            );
                        }
                    },
                    CancellationToken.None
                )
                .ContinueWith(t =>
                {
                    receiverTokenSource.Dispose();
                    scopeTokenSource.Dispose();
                });
#pragma warning restore 4014
        }
        else
        {
            //someone else has locked this instance, start timer to make sure the owner hasn't died
            _lockPollingEnabled = true;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lockManager.InitializeAsync().ConfigureAwait(false);
        await AcquireLock(cancellationToken).ConfigureAwait(false);

#pragma warning disable 4014
        _pollingLoop = Task.Run(
            async () => await TimerLoop(cancellationToken),
            CancellationToken.None
        );
#pragma warning restore 4014
    }
}
