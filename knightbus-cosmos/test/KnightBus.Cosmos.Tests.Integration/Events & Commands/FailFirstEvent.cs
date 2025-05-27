using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;

namespace KnightBus.Cosmos.Tests.Integration;


//Event where the first attempt to process each event will fail
public class FailFirstEvent : ICosmosEvent
{
    public required string Body { get; set;  }
}

class FailFirstMapping : IMessageMapping<FailFirstEvent>
{
    public string QueueName => "FailFirst";
}

class FailFirstSubscription : IEventSubscription<FailFirstEvent>
{
    public string Name => "FFSub";
}


class FailFirstProcessor :
    IProcessEvent<FailFirstEvent, FailFirstSubscription, CosmosProcessingSetting>
{
    public Task ProcessAsync(FailFirstEvent message, CancellationToken cancellationToken)
    {
        //Fail processing the first attempt of each event
        if (!ProcessedTracker.processed.TryGetValue(message.Body, out var value))
        {
            ProcessedTracker.Increment(message.Body);
            throw new Exception($"Simulated error the first time when processing");
        }
        else
        {
            return Task.CompletedTask;
        }
    }
}





