using System;

namespace KnightBus.Core.Management;

public class QueueProperties
{
    public QueueProperties(
        string name,
        IQueueManager manager,
        bool isLoaded,
        QueueType? queueType = null
    )
    {
        queueType ??= manager.QueueType;

        Name = name;
        Type = queueType.Value;
        Manager = manager;
        IsLoaded = isLoaded;
    }

    public string Name { get; internal set; }

    public QueueType Type { get; }
    public IQueueManager Manager { get; }
    public bool HasSubQueues => Type == QueueType.Topic;
    public bool IsLoaded { get; set; }

    public long TotalMessageCount { get; set; }
    public long ActiveMessageCount { get; init; }
    public long DeadLetterMessageCount { get; init; }
    public long ScheduledMessageCount { get; init; }
    public long TransferMessageCount { get; init; }
    public long TransferDeadLetterMessageCount { get; init; }
    public long SizeInBytes { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset AccessedAt { get; init; }
}
