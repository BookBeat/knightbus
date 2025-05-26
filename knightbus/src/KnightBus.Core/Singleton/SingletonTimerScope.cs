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

        _runningTask = Task.Run(async () => await TimerLoop(_cts.Token), _cts.Token);
    }

    private async Task TimerLoop(CancellationToken cancellationToken)
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
                _cts.Cancel();
                break;
            }
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
        try
        {
            if (!_cts.IsCancellationRequested)
                _cts.Cancel();
        }
        catch (Exception)
        {
            //Swallow
        }
        ;
        if (_lockHandle != null && _autoRelease)
        {
            _log.LogInformation("Releasing lock {LockHandle}", _lockHandle);
            _lockHandle.ReleaseAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
