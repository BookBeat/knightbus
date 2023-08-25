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
    private readonly CountdownEvent _event;


    public ExecutionCompletion(int count = 1)
    {

        _event = new CountdownEvent(count);
    }

    public void Complete()
    {
        _event.Signal();
    }

    public void Wait(TimeSpan timeout)
    {
        _event.WaitHandle.WaitOne(timeout);
    }

    public void Dispose()
    {
        _event?.Dispose();
    }
}
