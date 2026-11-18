namespace KnightBus.Azure.ServiceBus.Messages;

/// <summary>
/// Implement on an event subscription to choose, for that subscription only, whether it forwards
/// its messages to a queue of its own, and what that queue is called. It overrides the host-wide
/// <c>ServiceBusConfiguration.SubscriptionForwarding.Enabled</c>.
/// </summary>
public interface IServiceBusForwardingSubscription
{
    /// <summary>
    /// True to forward the subscription to its own queue and read from that queue, false to read
    /// from the subscription.
    /// </summary>
    bool ForwardToQueue { get; }

    /// <summary>
    /// The queue the subscription forwards to, or null for <c>{topic}.{subscription}</c>.
    /// </summary>
    /// <remarks>
    /// Changing it on a subscription that already forwards points the subscription at the new
    /// queue on the next start. Messages left on the old queue stay there until they are moved.
    /// </remarks>
    string? QueueName => null;
}
