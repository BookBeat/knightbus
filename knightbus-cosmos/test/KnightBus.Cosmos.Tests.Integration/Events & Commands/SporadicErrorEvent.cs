using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;

namespace KnightBus.Cosmos.Tests.Integration;

//Event with simulated errors
public class SporadicErrorEvent : ICosmosEvent
{
    public required string Body { get; set; }
}

class SporadicErrorMapping : IMessageMapping<SporadicErrorEvent>
{
    public string QueueName => "Random-Errors";
}

class SporadicErrorSubscription : IEventSubscription<SporadicErrorEvent>
{
    public string Name => "RE_sub";
}

class SporadicErrorProcessor
    : IProcessEvent<SporadicErrorEvent, SporadicErrorSubscription, CosmosProcessingSetting>
{
    private static readonly ThreadLocal<Random> LocalRng = new ThreadLocal<Random>(
        () => new Random()
    );

    public Task ProcessAsync(SporadicErrorEvent message, CancellationToken cancellationToken)
    {
        if (LocalRng.Value!.Next(0, 5) == 0)
        {
            throw new HttpRequestException("Simulated error");
        }
        ProcessedTracker.Increment(message.Body);
        return Task.CompletedTask;
    }
}
