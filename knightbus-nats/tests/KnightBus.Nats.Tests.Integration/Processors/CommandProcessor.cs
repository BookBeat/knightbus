using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Nats.Tests.Integration.Processors;

public class CommandProcessor : IProcessCommand<JetStreamCommand, JetStreamSettings>
{
    private readonly IExecutionCounter _executionCounter;

    public CommandProcessor(IExecutionCounter executionCounter)
    {
        _executionCounter = executionCounter;
    }
    public Task ProcessAsync(JetStreamCommand message, CancellationToken cancellationToken)
    {
        Console.WriteLine(message.Message);
        _executionCounter.Increment();
        return Task.CompletedTask;
    }
}
