using KnightBus.UI.Console.Providers;

namespace KnightBus.UI.Console;

public enum QueueType
{
    Queue,
    Topic,
    Subscription
}
public class QueueProperties
{
    public QueueProperties(string name, IQueueManager manager, bool isLoaded)
    {
        Name = name;
        Type = manager.QueueType;
        ProviderName = manager.DisplayName;
        Manager = manager;
        HasSubQueues = manager.QueueType == QueueType.Topic;
        IsLoaded = isLoaded;
    }

    public string Name { get; internal set; }

    public QueueType Type { get; }
    public string ProviderName { get; }
    public IQueueManager Manager { get; }
    public bool HasSubQueues { get; }
    public bool IsLoaded { get; }

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
}
