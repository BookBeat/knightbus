using System;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace KnightBus.Core.Singleton
{
    public class SingletonTimerScope : IDisposable
    {
        private readonly ILog _log;
        private readonly ISingletonLockHandle _lockHandle;
        private readonly bool _autoRelease; //clock drift makes triggers unstable for singleton use if the function is fast
        private readonly Timer _timer;

        public SingletonTimerScope(ILog log, ISingletonLockHandle lockHandle, bool autoRelease)
        {
            _log = log;
            _lockHandle = lockHandle;
            _autoRelease = autoRelease;
            _timer = new Timer
            {
                AutoReset = true,
                Interval = TimeSpan.FromSeconds(30).TotalMilliseconds
            };
            _timer.Elapsed += Timer_Elapsed;
            _timer.Enabled = true;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            RenewLock();
        }

        private void RenewLock()
        {
            long delay = 0;
            var retries = 3;
            while (true)
            {
                if (retries == 0) return;
                var success = _lockHandle.RenewAsync(_log, CancellationToken.None).GetAwaiter().GetResult();
                if (!success)
                {
                    delay = delay + 1000;
                    retries = retries - 1;
                    continue;
                }
                break;
            }
        }

        public void Dispose()
        {
            _timer.Enabled = false;
            _timer.Dispose();
            if (_lockHandle != null && _autoRelease)
            {
                _log.Information("Releasing lock {LockHandle}", _lockHandle);
                _lockHandle.ReleaseAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        }
    }
}