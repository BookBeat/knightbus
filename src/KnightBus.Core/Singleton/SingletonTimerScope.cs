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
    private readonly object _releaseLockObject = new();
    private Task _runningTask;
    private Task? _releaseTask;

    /// <summary>
    /// Completes when the renewal loop has stopped and the lock release has finished
    /// </summary>
    public Task Completion => _runningTask;

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

        //Capture the token before scheduling and use no scheduling token: the owner can
        //cancel and dispose the source before the task starts, and reading _cts.Token after
        //disposal throws, which would keep the loop from ever running or releasing the lock
        var cancellationToken = _cts.Token;
        _runningTask = Task.Run(
            async () => await TimerLoop(cancellationToken),
            CancellationToken.None
        );
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
        catch (Exception)
        {
            //The owner of the CancellationTokenSource can already have disposed it, and
            //cancellation callbacks registered by consumers can throw
        }
    }

    private Task ReleaseLock()
    {
        //The loop and Dispose can race here, both must wait for the same single release
        lock (_releaseLockObject)
        {
            _releaseTask ??= ReleaseLockInternal();
        }
        return _releaseTask;
    }

    private async Task ReleaseLockInternal()
    {
        if (_lockHandle == null || !_autoRelease)
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
