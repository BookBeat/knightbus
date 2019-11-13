using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;

namespace KnightBus.Host.Singleton
{
    internal class SingletonChannelReceiver : IChannelReceiver
    {
        private readonly IChannelReceiver _channelReceiver;
        private readonly ISingletonLockManager _lockManager;
        private readonly ILog _log;
        private SingletonTimerScope _singletonScope;
        private readonly string _lockId;
        public IProcessingSettings Settings { get; set; }
        internal TimeSpan TimerInterval { get; set; } = TimeSpan.FromMinutes(1);
        private bool _enabled = false;
        private Task _pollingLoop;

        public SingletonChannelReceiver(IChannelReceiver channelReceiver, ISingletonLockManager lockManager, ILog log)
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
                if (_enabled)
                {
                    await AcquireLock(cancellationToken).ConfigureAwait(false);
                }

                await Task.Delay(TimerInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task AcquireLock(CancellationToken cancellationToken)
        {
            //Try and get the lock
            var lockHandle = await _lockManager.TryLockAsync(_lockId, TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);

            if (lockHandle != null)
            {
                var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _singletonScope = new SingletonTimerScope(_log, lockHandle, true, linkedTokenSource);
                _log.Information("Starting Singleton Processor with name {ProcessorName}", _lockId);
                await _channelReceiver.StartAsync(linkedTokenSource.Token).ConfigureAwait(false);
                _enabled = false;

#pragma warning disable 4014
                Task.Run(() =>
                {
                    linkedTokenSource.Token.WaitHandle.WaitOne();
                    //Stop signal received, restart the polling
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _enabled = true;
                        _log.Information("Singleton Processor with name {ProcessorName} lost its lock", _lockId);
                    }
                    
                }, cancellationToken);
#pragma warning restore 4014
            }
            else
            {
                //someone else has locked this instance, start timer to make sure the owner hasn't died
                _enabled = true;
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
}