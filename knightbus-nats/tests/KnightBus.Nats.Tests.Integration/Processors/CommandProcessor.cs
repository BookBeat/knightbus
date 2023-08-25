using KnightBus.Messages;

namespace KnightBus.Nats.Tests.Integration.Processors;

// public class CommandProcessor : IProcessCommand<JetStreamCommand, JetStreamSettings>
// {
//     private readonly IExecutionCounter _executionCounter;
//     private readonly IExecutionCompletion _executionCompletion;
//
//     public CommandProcessor(IExecutionCounter executionCounter, IExecutionCompletion executionCompletion)
//     {
//         _executionCounter = executionCounter;
//         _executionCompletion = executionCompletion;
//     }
//     public Task ProcessAsync(JetStreamCommand message, CancellationToken cancellationToken)
//     {
//         try
//         {
//             Console.WriteLine(message.Message);
//             _executionCounter.Increment();
//             return Task.CompletedTask;
//         }
//         finally
//         {
//             _executionCompletion.Complete();
//         }
//     }
// }

public class SubOne : IEventSubscription<JetStreamEvent>
{
    public string Name => "one";
}
public class SubTwo : IEventSubscription<JetStreamEvent>
{
    public string Name => "two";
}
