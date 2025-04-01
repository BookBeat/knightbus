using KnightBus.Core;
using KnightBus.Cosmos.Messages;
using KnightBus.Messages;

namespace KnightBus.Cosmos.Tests.EndToEnd;

class CosmosEventProcessor :
    IProcessEvent<OneSubCosmosEvent, SampleSubscription, CosmosProcessingSetting>,
    IProcessEvent<TwoSubCosmosEvent, Subscription1, CosmosProcessingSetting>,
    IProcessEvent<TwoSubCosmosEvent, Subscription2, CosmosProcessingSetting>,
    IProcessEvent<PoisonEvent, PoisonSubscription, CosmosProcessingSetting>
{
    public Task ProcessAsync(OneSubCosmosEvent message, CancellationToken cancellationToken)
    {
        if (message.MessageBody != null)
        {
            ProcessedMessages.Queue.Enqueue(message.MessageBody);
        }

        return Task.CompletedTask;
    }

    public Task ProcessAsync(TwoSubCosmosEvent message, CancellationToken cancellationToken)
    {
        if (message.MessageBody != null)
        {
            ProcessedMessages.Queue.Enqueue(message.MessageBody);
        }

        return Task.CompletedTask;
    }

    public Task ProcessAsync(PoisonEvent message, CancellationToken cancellationToken)
    {
        if (message.Body != null)
        {
            ProcessedMessages.Queue.Enqueue(message.Body);
        }
        throw new HttpRequestException("Simulated error");
    }
}

class CosmosProcessingSetting : IProcessingSettings
{
    public int MaxConcurrentCalls => 10; //Currently not used
    public int PrefetchCount => 50; //Currently not used
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5); //Currently not used
    public int DeadLetterDeliveryLimit => 2;
}

public class OneSubCosmosEvent : ICosmosEvent
{
    public string? MessageBody { get; set;  }
}
class OneSubCosmosEventMapping : IMessageMapping<OneSubCosmosEvent>
{
    public string QueueName => "test-topic";
}
class SampleSubscription: IEventSubscription<OneSubCosmosEvent>
{
    public string Name => "sample_subscription";
}

public class TwoSubCosmosEvent : ICosmosEvent
{
    public string? MessageBody { get; set;  }
}
class TwoSubCosmosEventMapping : IMessageMapping<TwoSubCosmosEvent>
{
    public string QueueName => "other-topic";
}
class Subscription1: IEventSubscription<TwoSubCosmosEvent>
{
    public string Name => "subscription_1";
}

class Subscription2: IEventSubscription<TwoSubCosmosEvent>
{
    public string Name => "subscription_2";
}

public class PoisonEvent : ICosmosEvent
{
    public string? Body { get; set;  }
}

class PoisonEventMapping : IMessageMapping<PoisonEvent>
{
    public string QueueName => "poison-topic";
}

class PoisonSubscription : IEventSubscription<PoisonEvent>
{
    public string Name => "poison_subscription";
}
