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
    private readonly InFlightMessages? _inFlight;

    //The lock this receiver holds. Only the polling loop changes it, after StartAsync has made the
    //first attempt, so the state of one acquisition is never mixed with the next
    private volatile Held? _held;

    private sealed record Held(
        SingletonTimerScope Scope,
        CancellationTokenSource ReceiverSource,
        CancellationTokenSource ScopeSource,
        long AcquiredAt
    );

    private readonly string _lockId;
    internal string LockId => _lockId;

    //How heavy this processor is when the locks are spread over the hosts
    internal int Weight { get; init; } = 1;

    //Identifies this host in the logs, the same id the other hosts see in the placement
    private string HostId => _placement?.HostId ?? Environment.MachineName;
    public IProcessingSettings Settings { get; set; }
    internal TimeSpan TimerInterval { get; set; } = TimeSpan.FromMinutes(1);
    internal TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(1);
    internal TimeSpan LockRefreshInterval { get; set; } = TimeSpan.FromSeconds(19);

    //Share of TimerInterval added at random to each wait, 0 means a fixed interval
    internal double PollJitter { get; set; }

    //How long a lock must have been seen free before a host that is not the preferred one takes it,
    //which covers a preferred host that is alive but not taking the lock
    internal TimeSpan TakeoverGrace { get; set; } = TimeSpan.FromMinutes(2);

    //The shortest time a lock is held before it is handed over, so locks do not move back and forth
    internal TimeSpan HandoffInterval { get; set; } = TimeSpan.FromMinutes(10);

    //How often a host looks for a lock it should hold but does not, so a lock that is handed over
    //or released does not sit idle for a whole poll interval
    internal TimeSpan PreferredPollInterval { get; set; } = TimeSpan.FromSeconds(5);

    internal TimeProvider Time { get; set; } = TimeProvider.System;

    //When the lock was first seen free while another host is the one that should hold it
    private long? _freeSince;
    private Task? _pollingLoop;

    /// <summary>
    /// Completes when the lock held by this receiver has been released
    /// </summary>
    internal Task TeardownCompletion => _held?.Scope.Completion ?? Task.CompletedTask;

    public SingletonChannelReceiver(
        IChannelReceiver channelReceiver,
        ISingletonLockManager lockManager,
        ILogger log,
        string? lockId = null,
        CancellationToken? teardownToken = null,
        ISingletonPlacement? placement = null,
        InFlightMessages? inFlight = null
    )
    {
        _placement = placement;
        _inFlight = inFlight;
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
            try
            {
                await PollOnce(cancellationToken).ConfigureAwait(false);
                await Task.Delay(
                        NextPollDelay(CurrentPollInterval(), PollJitter),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                //A failed attempt must not end the loop, or this host never takes the lock again
                _log.LogError(
                    e,
                    "Singleton Processor with name {ProcessorName} on host {HostId} failed to check its lock",
                    _lockId,
                    HostId
                );
                await Task.Delay(
                        NextPollDelay(CurrentPollInterval(), PollJitter),
                        cancellationToken
                    )
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    private async Task PollOnce(CancellationToken cancellationToken)
    {
        var held = _held;
        if (held is { Scope.Completion.IsCompleted: true })
        {
            //The scope ends by itself only when the lock could not be renewed
            _log.LogInformation(
                "Singleton Processor with name {ProcessorName} lost its lock on host {HostId}",
                _lockId,
                HostId
            );
            _held = held = null;
        }

        if (held == null)
        {
            //The receiver was cancelled with the lock, but its handlers may still be running and
            //must not overlap with the ones a new receiver would start
            if (_inFlight != null)
                await _inFlight.WaitUntilIdleAsync(cancellationToken).ConfigureAwait(false);
            await AcquireLock(cancellationToken).ConfigureAwait(false);
        }
        else if (held.ScopeSource.IsCancellationRequested)
            return;
        else if (held.ReceiverSource.IsCancellationRequested)
            //A hand-over that did not finish has already stopped the receiver, so it is finished
            //whatever the advice says now
            await HandOver(held, cancellationToken).ConfigureAwait(false);
        else
            await HandOverIfNotPreferred(held, cancellationToken).ConfigureAwait(false);
    }

    private TimeSpan CurrentPollInterval() =>
        _held == null
        && _placement != null
        && _placement.Advise(_lockId) == _placement.HostId
        && PreferredPollInterval < TimerInterval
            ? PreferredPollInterval
            : TimerInterval;

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
        var preferred = _placement?.Advise(_lockId);
        if (preferred == null || preferred == _placement!.HostId)
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
            if (await inspector.IsLockedAsync(_lockId, cancellationToken).ConfigureAwait(false))
            {
                _freeSince = null;
                return false;
            }
        }
        catch (Exception e)
            when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _log.LogWarning(e, "Could not tell whether {ProcessorName} is locked", _lockId);
            _freeSince = null;
            return false;
        }

        _freeSince ??= Time.GetTimestamp();
        return Time.GetElapsedTime(_freeSince.Value) >= TakeoverGrace;
    }

    private async Task HandOverIfNotPreferred(Held held, CancellationToken cancellationToken)
    {
        if (_placement == null)
            return;
        var preferred = _placement.Advise(_lockId);
        if (preferred == null || preferred == _placement.HostId)
            return;
        if (Time.GetElapsedTime(held.AcquiredAt) < HandoffInterval)
            return;

        _log.LogInformation(
            "Singleton Processor with name {ProcessorName} on host {HostId} hands its lock over to {PreferredHost}",
            _lockId,
            HostId,
            preferred
        );
        await HandOver(held, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandOver(Held held, CancellationToken cancellationToken)
    {
        //Let the message being processed finish without taking new ones. It may take as long as
        //the message lock lasts, which the transport enforces anyway.
        if (_channelReceiver is IDrainableChannelReceiver drainable)
        {
            var drainTimeout = Settings.MessageLockTimeout;
            using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            drain.CancelAfter(drainTimeout);
            try
            {
                await drainable.StopFetchingAsync(drain.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _log.LogWarning(
                    "Singleton Processor with name {ProcessorName} did not finish its message in {Timeout}, cancelling it",
                    _lockId,
                    drainTimeout
                );
            }
        }

        //Stop the wrapped receiver and whatever it is still running, then give the lock up once the
        //cancelled messages have stopped, or the next holder would process while they are still
        //running. A handler that never stops keeps the lock where it is
        TryCancel(held.ReceiverSource);
        if (_inFlight != null)
            await _inFlight.WaitUntilIdleAsync(cancellationToken).ConfigureAwait(false);
        TryCancel(held.ScopeSource);
        await held.Scope.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        _held = null;
    }

    private void TryCancel(CancellationTokenSource source)
    {
        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            //The lock was already lost and the sources cleaned up
        }
        catch (Exception e)
        {
            //A callback registered on the token failed, the token is cancelled all the same
            _log.LogWarning(
                e,
                "Singleton Processor with name {ProcessorName} on host {HostId} had an error while stopping",
                _lockId,
                HostId
            );
        }
    }

    private async Task AcquireLock(CancellationToken cancellationToken)
    {
        if (!await MayTryToAcquire(cancellationToken).ConfigureAwait(false))
            return;

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
            _freeSince = null;
            var scope = new SingletonTimerScope(
                _log,
                lockHandle,
                true,
                LockRefreshInterval,
                scopeTokenSource
            );
            //The scope completes once the lock is released, or given up when the release failed
            _ = scope.Completion.ContinueWith(
                _ =>
                {
                    _log.LogInformation(
                        "Singleton Processor with name {ProcessorName} on host {HostId} stopped holding its lock",
                        _lockId,
                        HostId
                    );
                    receiverTokenSource.Dispose();
                    scopeTokenSource.Dispose();
                },
                TaskScheduler.Default
            );
            _held = new Held(scope, receiverTokenSource, scopeTokenSource, Time.GetTimestamp());
            _log.LogInformation(
                "Starting Singleton Processor with name {ProcessorName} on host {HostId}",
                _lockId,
                HostId
            );
            try
            {
                await _channelReceiver.StartAsync(receiverTokenSource.Token).ConfigureAwait(false);
            }
            catch
            {
                //Holding the lock without processing would keep every other host out
                TryCancel(scopeTokenSource);
                throw;
            }
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
