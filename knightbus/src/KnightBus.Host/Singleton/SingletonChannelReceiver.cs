using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Timer = System.Timers.Timer;

namespace KnightBus.Host.Singleton
{
    internal class SingletonChannelReceiver : IChannelReceiver
    {
        private Timer _timer;
        private readonly IChannelReceiver _channelReceiver;
        private readonly ISingletonLockManager _lockManager;
        private ILog _log;
        private SingletonTimerScope _singletonScope;
        private readonly string _lockId;
        public IProcessingSettings Settings { get; set; }
        public ITransportConfiguration Configuration { get; }
        internal double TimerIntervalMs { get; set; } = TimeSpan.FromMinutes(1).TotalMilliseconds;

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

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Enabled = false;
            StartAsync().GetAwaiter().GetResult();
        }

        public async Task StartAsync()
        {
            await _lockManager.InitializeAsync().ConfigureAwait(false);
            if (_timer == null)
            {
                _timer = new Timer
                {
                    AutoReset = true,
                    Interval = TimerIntervalMs
                };
                _timer.Elapsed += TimerElapsed;
            }
            //Try and get the lock
            var lockHandle = await _lockManager.TryLockAsync(_lockId, TimeSpan.FromSeconds(60), CancellationToken.None).ConfigureAwait(false);

            if (lockHandle != null)
            {
                _singletonScope = new SingletonTimerScope(_log, lockHandle, true);
                _log.Information("Starting Singleton Processor with name {ProcessorName}", _lockId);
                await _channelReceiver.StartAsync().ConfigureAwait(false);
            }
            else
            {
                //someone else has locked this instance, start timer to make sure the owner hasn't died
                _timer.Enabled = true;
            }
        }
    }
}