using KnightBus.Azure.ServiceBus;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Messages;

namespace KnightBus.Examples.Azure.ServiceBus;

public class OtherSampleServiceBusMessage : IServiceBusCommand
{
    public string SomeProperty { get; set; }
}

class SampleServiceBusEventMapping : IMessageMapping<SampleServiceBusEvent>
{
    public string QueueName => "your-topic";
}

class SampleServiceBusMessageMapping
    : IMessageMapping<SampleServiceBusMessage>,
        IServiceBusCreationOptions
{
    public string QueueName => "your-queue";
    public bool EnablePartitioning => true;
    public bool SupportOrdering => false;
    public bool EnableBatchedOperations => true;
}

public class SampleServiceBusMessage : IServiceBusCommand
{
    public string Message { get; set; }
}

public class SampleServiceBusEvent : IServiceBusEvent
{
    public string Message { get; set; }
}

public class OtherSampleServiceBusMessageMapping : IMessageMapping<OtherSampleServiceBusMessage>
{
    public string QueueName => "other-queue";
}

public class EventSubscriptionOne : IEventSubscription<SampleServiceBusEvent>
{
    public string Name => "subscription-1";
}

public class EventSubscriptionTwo : IEventSubscription<SampleServiceBusEvent>
{
    public string Name => "subscription-2";
}
