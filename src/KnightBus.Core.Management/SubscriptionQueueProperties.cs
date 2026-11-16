namespace KnightBus.Core.Management;

public class SubscriptionQueueProperties : QueueProperties
{
    public SubscriptionQueueProperties(
        string name,
        IQueueManager manager,
        string topic,
        bool isLoaded
    )
        : base(name, manager, isLoaded, QueueType.Subscription)
    {
        Topic = topic;
    }

    public string Topic { get; }

    /// <summary>
    /// The queue this subscription forwards its messages to, or null when it is read directly.
    /// Dead letters of a forwarded subscription end up on that queue.
    /// </summary>
    public string? ForwardTo { get; init; }
}
