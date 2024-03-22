namespace KnightBus.Core.Management;

public class SubscriptionQueueProperties : QueueProperties
{
    public SubscriptionQueueProperties(string name, IQueueManager manager, string topic, bool isLoaded) : base(name, manager, isLoaded, QueueType.Subscription)
    {
        Topic = topic;
    }

    public string Topic { get; }
}
