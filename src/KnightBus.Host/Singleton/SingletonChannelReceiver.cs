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
    private readonly ISingletonPlacement? _placement;
    private readonly SingletonPlacementOptions? _placementOptions;
    private SingletonTimerScope? _singletonScope;
    private CancellationTokenSource? _scopeTokenSource;
    private CancellationTokenSource? _receiverTokenSource;
    private readonly string _lockId;
    internal string LockId => _lockId;
    public IProcessingSettings Settings { get; set; }
    internal TimeSpan TimerInterval { get; set; } = TimeSpan.FromMinutes(1);
    internal TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(1);
    internal TimeSpan LockRefreshInterval { get; set; } = TimeSpan.FromSeconds(19);

    //Share of TimerInterval added at random to each wait, 0 means a fixed interval
    internal double PollJitter { get; set; }

    internal TimeProvider Time { get; set; } = TimeProvider.System;

    //When the lock was first seen free while another host is the one that should hold it
    private long? _freeSince;
    private long _acquiredAt;

    //Set when the lock is released on purpose, so the watcher does not report it as lost
    private volatile bool _handingOver;

    //How long a handing over host waits for the wrapped receiver to unwind after cancelling it,
    //so the next holder does not start while the previous handlers are still running
    private static readonly TimeSpan UnwindTimeout = TimeSpan.FromSeconds(10);

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
        CancellationToken? teardownToken = null,
        ISingletonPlacement? placement = null,
        SingletonPlacementOptions? placementOptions = null
    )
    {
        _placement = placement;
        _placementOptions = placementOptions;
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
            else
            {
                await HandOverIfNotPreferred(cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(NextPollDelay(TimerInterval, PollJitter), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal static TimeSpan NextPollDelay(TimeSpan interval, double jitter, Random? random = null)
    {
        if (jitter <= 0)
            return interval;
        return interval + interval * ((random ?? Random.Shared).NextDouble() * jitter);
    }

    /// <summary>
    /// False while another host is the one that should hold the lock and has not had its grace
    /// period to take it. Without placement, or when it has no information, every host may try.
    /// </summary>
    private async Task<bool> MayTryToAcquire(CancellationToken cancellationToken)
    {
        var advice = _placement?.Advise(_lockId);
        if (advice == null || advice.PreferredIsSelf)
        {
            _freeSince = null;
            return true;
        }

        //Another host should have it. Take it only when it has been free for the grace period,
        //which means the preferred host is not taking it. Without a way to see whether the lock
        //is held that cannot be known, so the lock is left to the preferred host
        if (_lockManager is not ISingletonLockInspector inspector)
            return false;
        try
        {
            if (await inspector.IsHeldAsync(_lockId, cancellationToken).ConfigureAwait(false))
            {
                _freeSince = null;
                return false;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _log.LogWarning(e, "Could not tell whether {ProcessorName} is locked", _lockId);
            _freeSince = null;
            return false;
        }

        _freeSince ??= Time.GetTimestamp();
        return Time.GetElapsedTime(_freeSince.Value) >= _placementOptions!.TakeoverGrace;
    }

    private async Task HandOverIfNotPreferred(CancellationToken cancellationToken)
    {
        if (_placement == null || _placementOptions == null || _scopeTokenSource == null)
            return;
        var advice = _placement.Advise(_lockId);
        if (advice == null || advice.PreferredIsSelf)
            return;
        if (
            advice.PreferredHostMemberFor < _placementOptions.StabilityWindow
            || Time.GetElapsedTime(_acquiredAt) < _placementOptions.HandoffInterval
        )
            return;

        _log.LogInformation(
            "Singleton Processor with name {ProcessorName} hands its lock over to {Host}",
            _lockId,
            advice.PreferredHost
        );
        await HandOver(cancellationToken).ConfigureAwait(false);
    }

    private async Task HandOver(CancellationToken cancellationToken)
    {
        var scopeSource = _scopeTokenSource!;
        var receiverSource = _receiverTokenSource!;
        var scope = _singletonScope!;
        _handingOver = true;

        //Let the message being processed finish, up to the drain timeout, without taking new ones
        if (_channelReceiver is IDrainableChannelReceiver drainable)
        {
            using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            drain.CancelAfter(_placementOptions!.DrainTimeout);
            try
            {
                await drainable.StopFetchingAsync(drain.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _log.LogWarning(
                    "Singleton Processor with name {ProcessorName} did not finish its message in {Timeout}, cancelling it",
                    _lockId,
                    _placementOptions.DrainTimeout
                );
            }
        }

        //Stop the wrapped receiver and whatever it is still running, then give the lock up
        TryCancel(receiverSource);
        if (_channelReceiver is IDrainableChannelReceiver unwinding)
        {
            using var unwind = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            unwind.CancelAfter(UnwindTimeout);
            try
            {
                await unwinding.StopFetchingAsync(unwind.Token).ConfigureAwait(false);
            }
            catch (Exception e)
                when (e is not OperationCanceledException
                    || !cancellationToken.IsCancellationRequested
                )
            {
                //The wrapped receiver may already be closed, the lock is released either way
            }
        }

        TryCancel(scopeSource);
        await scope.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void TryCancel(CancellationTokenSource source)
    {
        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            //The lock was already lost and the sources cleaned up
        }
    }

    private async Task AcquireLock(CancellationToken cancellationToken)
    {
        if (!await MayTryToAcquire(cancellationToken).ConfigureAwait(false))
        {
            _lockPollingEnabled = true;
            return;
        }

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
            _scopeTokenSource = scopeTokenSource;
            _receiverTokenSource = receiverTokenSource;
            _acquiredAt = Time.GetTimestamp();
            _freeSince = null;
            _handingOver = false;
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
                            if (!_handingOver)
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
