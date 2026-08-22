using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KnightBus.Core.Singleton;

public class SingletonTimerScope : IDisposable
{
    private readonly ILogger _log;
    private readonly ISingletonLockHandle _lockHandle;
    private readonly bool _autoRelease; //clock drift makes triggers unstable for singleton use if the function is fast
    private readonly TimeSpan _renewalInterval;
    private readonly CancellationTokenSource _cts;
    private Task _runningTask;
    private int _lockReleased;

    public SingletonTimerScope(
        ILogger log,
        ISingletonLockHandle lockHandle,
        bool autoRelease,
        TimeSpan renewalInterval,
        CancellationTokenSource cancellationTokenSource
    )
    {
        _log = log;
        _lockHandle = lockHandle;
        _autoRelease = autoRelease;
        _renewalInterval = renewalInterval;
        _cts = cancellationTokenSource;

        //No scheduling token: the loop must always run so the lock is released even when
        //cancellation was requested before the task started
        _runningTask = Task.Run(async () => await TimerLoop(_cts.Token), CancellationToken.None);
    }

    private async Task TimerLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RenewLock(cancellationToken).ConfigureAwait(false);
                    await Task.Delay(_renewalInterval, cancellationToken);
                }
                catch (Exception)
                {
                    //Stop execution
                    TryCancel();
                    break;
                }
            }
        }
        finally
        {
            //Release the lock when the loop stops, so another instance can take over
            //immediately instead of waiting for the lock to expire
            await ReleaseLock().ConfigureAwait(false);
        }
    }

    private void TryCancel()
    {
        try
        {
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            //The owner of the CancellationTokenSource has already disposed it
        }
    }

    private async Task ReleaseLock()
    {
        if (_lockHandle == null || !_autoRelease)
            return;
        //Only release once, the loop and Dispose can race here
        if (Interlocked.Exchange(ref _lockReleased, 1) == 1)
            return;
        try
        {
            _log.LogInformation("Releasing lock {LockHandle}", _lockHandle);
            await _lockHandle.ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            //The lock will expire by itself when it cannot be released
            _log.LogWarning(e, "Failed to release lock {LockHandle}", _lockHandle);
        }
    }

    private async Task RenewLock(CancellationToken cancellationToken)
    {
        var delay = 0;
        var retries = 3;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (retries == 0)
                return;
            var exit = await _lockHandle.RenewAsync(_log, cancellationToken).ConfigureAwait(false);
            if (exit)
                return;

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            delay += 1000;
            retries -= 1;
        }
    }

    public void Dispose()
    {
        TryCancel();
        ReleaseLock().GetAwaiter().GetResult();
    }
}
