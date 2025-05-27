using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;

namespace KnightBus.Cosmos.Tests.Integration;


public class PoisonEvent : ICosmosEvent
{
    public required string Body { get; set;  }
}

class PoisonEventMapping : IMessageMapping<PoisonEvent>
{
    public string QueueName => "poison-topic";
}

class PoisonSubscription : IEventSubscription<PoisonEvent>
{
    public string Name => "poison_subscription";
}

class PoisonEventProcessor :

    IProcessEvent<PoisonEvent, PoisonSubscription, CosmosProcessingSetting>
{
    public Task ProcessAsync(PoisonEvent message, CancellationToken cancellationToken)
    {
        ProcessedTracker.Increment(message.Body);
        throw new HttpRequestException("Simulated error");
    }
}
