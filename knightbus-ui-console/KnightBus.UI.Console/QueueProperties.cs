namespace KnightBus.UI.Console;

public enum QueueType
{
    Queue,
    Topic,
    Subscription
}
public class QueueProperties
{
    public QueueProperties(string name, QueueType type, IQueueManager manager)
    {
        Name = name;
        Type = type;
        Manager = manager;
    }

    public QueueProperties(string name, QueueType type, IQueueManager manager, Func<IEnumerable<QueueProperties>> getSubQueues) : this(name, type, manager)
    {
        GetSubQueues = getSubQueues;
        HasSubQueues = true;
    }

    public string Name { get; internal set; }

    public QueueType Type { get; }
    public IQueueManager Manager { get; }
    public bool HasSubQueues { get; }

    public long TotalMessageCount { get; internal set; }
    public long ActiveMessageCount { get; internal init; }
    public long DeadLetterMessageCount { get; internal init; }
    public long ScheduledMessageCount { get; internal init; }
    public long TransferMessageCount { get; internal init; }
    public long TransferDeadLetterMessageCount { get; internal init; }
    public long SizeInBytes { get; internal init; }
    public DateTimeOffset CreatedAt { get; internal init; }
    public DateTimeOffset UpdatedAt { get; internal init; }
    public DateTimeOffset AccessedAt { get; internal init; }
    public Func<IEnumerable<QueueProperties>> GetSubQueues;
}
