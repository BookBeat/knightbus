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
    private SingletonTimerScope _singletonScope;
    private readonly string _lockId;
    public IProcessingSettings Settings { get; set; }
    internal TimeSpan TimerInterval { get; set; } = TimeSpan.FromMinutes(1);
    internal TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(1);
    internal TimeSpan LockRefreshInterval { get; set; } = TimeSpan.FromSeconds(19);
    private bool _lockPollingEnabled = false;
    private Task _pollingLoop;

    public SingletonChannelReceiver(IChannelReceiver channelReceiver, ISingletonLockManager lockManager, ILogger log)
    {
        _channelReceiver = channelReceiver;
        _lockManager = lockManager;
        _log = log;
        _lockId = channelReceiver.GetType().FullName;
        //MaxConcurrent and Prefetch must have specific  values to work with a singleton implementation.
        //Override those and let the other values be set from the specific implementation
        Settings = new SingletonProcessingSettings
        {
            MessageLockTimeout = _channelReceiver.Settings.MessageLockTimeout,
            DeadLetterDeliveryLimit = _channelReceiver.Settings.DeadLetterDeliveryLimit
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
        var lockHandle = await _lockManager.TryLockAsync(_lockId, LockDuration, cancellationToken).ConfigureAwait(false);

        if (lockHandle != null)
        {
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _singletonScope = new SingletonTimerScope(_log, lockHandle, true, LockRefreshInterval, linkedTokenSource);
            _log.LogInformation("Starting Singleton Processor with name {ProcessorName}", _lockId);
            await _channelReceiver.StartAsync(linkedTokenSource.Token).ConfigureAwait(false);
            _lockPollingEnabled = false;

#pragma warning disable 4014
            Task.Run(() =>
            {
                linkedTokenSource.Token.WaitHandle.WaitOne();
                //Stop signal received, restart the polling
                if (!cancellationToken.IsCancellationRequested)
                {
                    _lockPollingEnabled = true;
                    _log.LogInformation("Singleton Processor with name {ProcessorName} lost its lock", _lockId);
                }

            }, cancellationToken).ContinueWith(t => linkedTokenSource.Dispose());
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
        _pollingLoop = Task.Run(async () => await TimerLoop(cancellationToken), cancellationToken);
#pragma warning restore 4014
    }
}
