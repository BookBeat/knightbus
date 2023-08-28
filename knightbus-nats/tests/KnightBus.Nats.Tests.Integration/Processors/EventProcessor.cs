using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Nats.Tests.Integration.Processors;

public class EventProcessor :
    IProcessEvent<JetStreamEvent, SubOne, JetStreamSettings>,
    IProcessEvent<JetStreamEvent, SubTwo, JetStreamSettings>
{
    private readonly IExecutionCounter _executionCounter;
    private readonly IExecutionCompletion _executionCompletion;

    public EventProcessor(IExecutionCounter executionCounter, IExecutionCompletion executionCompletion)
    {
        _executionCounter = executionCounter;
        _executionCompletion = executionCompletion;
    }

    public Task ProcessAsync(JetStreamEvent message, CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine(message.Message);
            if (message.Throw) throw new Exception("kaboom");
            _executionCounter.Increment();
            return Task.CompletedTask;
        }
        finally
        {
            _executionCompletion.Complete();
        }
    }
}
public class SubOne : IEventSubscription<JetStreamEvent>
{
    public string Name => "one";
}
public class SubTwo : IEventSubscription<JetStreamEvent>
{
    public string Name => "two";
}
