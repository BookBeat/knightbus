using KnightBus.Messages;

namespace KnightBus.Health;

public interface IRemediationMapping
{
    string? RemediationMessage { get; }
}

public interface IRemediateMessageMapping<T> : IRemediationMapping
    where T : IMessage { }

public interface IRemediateEventSubscriptionMapping<T> : IRemediationMapping
    where T : IEventSubscription { }
