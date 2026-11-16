namespace KnightBus.Azure.ServiceBus.Messages;

/// <summary>
/// Implement on an event subscription to choose, for that subscription only, whether it forwards
/// its messages to a queue of its own. It overrides the host-wide
/// <c>ServiceBusConfiguration.SubscriptionForwarding.Enabled</c>.
/// </summary>
public interface IServiceBusForwardingSubscription
{
    /// <summary>
    /// True to forward the subscription to its own queue and read from that queue, false to read
    /// from the subscription.
    /// </summary>
    bool ForwardToQueue { get; }
}
