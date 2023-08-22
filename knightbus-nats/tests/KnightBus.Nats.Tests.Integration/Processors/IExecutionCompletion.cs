using System;
using System.Collections.Concurrent;
using System.Threading;

namespace KnightBus.Nats.Tests.Integration.Processors;

public interface IExecutionCompletion
{
    void Wait(TimeSpan timeout);
    void Complete();
}


public class ExecutionCompletion : IExecutionCompletion, IDisposable
{
    private readonly ConcurrentBag<CancellationTokenSource> _completed = new();

    public ExecutionCompletion(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            _completed.Add(new CancellationTokenSource());
        }
    }

    public void Complete()
    {
        foreach (var cts in _completed)
        {
            if (!cts.IsCancellationRequested)
                cts.Cancel();
        }
    }

    public void Wait(TimeSpan timeout)
    {
        foreach (var cts in _completed)
        {
            cts.Token.WaitHandle.WaitOne(timeout);
        }
    }

    public void Dispose()
    {
        foreach (var cts in _completed)
        {
            cts?.Dispose();
        }
    }
}
