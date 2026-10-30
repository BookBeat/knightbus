using KnightBus.Messages;

namespace KnightBus.NewRelic.Tests.Unit;

public class TestCommand : ICommand { }

public class TestCommandMapping : IMessageMapping<TestCommand>
{
    public string QueueName => "test-command-queue";
}

public class TestEvent : IEvent { }

public class TestEventMapping : IMessageMapping<TestEvent>
{
    public string QueueName => "test-event-topic";
}

public class TestEventSubscription : IEventSubscription<TestEvent>
{
    public string Name => "test-subscription";
}

public class UnmappedCommand : ICommand { }

public class UnmappedEvent : IEvent { }
