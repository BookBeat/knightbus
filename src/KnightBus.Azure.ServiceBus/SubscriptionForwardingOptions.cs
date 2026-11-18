namespace KnightBus.Azure.ServiceBus;

/// <summary>
/// Controls whether event subscriptions forward their messages to a queue of their own, which the
/// subscriber then reads instead of the subscription.
/// </summary>
/// <remarks>
/// With forwarding, a dead letter ends up on the subscriber's own queue, so re-sending it reaches
/// only that subscriber instead of every subscription on the topic. Turning it on changes where the
/// subscriber reads from, so it is off by default. See the Azure Service Bus transport docs before
/// enabling it on an existing subscription.
/// </remarks>
public class SubscriptionForwardingOptions
{
    /// <summary>
    /// Forward every subscription this host processes to its own queue. A subscription that
    /// implements <see cref="Messages.IServiceBusForwardingSubscription"/> overrides this value.
    /// </summary>
    /// <remarks>Defaults to false.</remarks>
    public bool Enabled { get; set; }
}
