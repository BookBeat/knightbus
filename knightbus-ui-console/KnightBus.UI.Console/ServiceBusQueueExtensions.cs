using Azure.Messaging.ServiceBus.Administration;

namespace KnightBus.UI.Console;

public static class ServiceBusQueueExtensions
{
    public static QueueProperties ToQueueProperties(this QueueRuntimeProperties properties)
    {
        return new QueueProperties(properties.Name, QueueType.Queue)
        {
            ActiveMessageCount = properties.ActiveMessageCount,
            TotalMessageCount = properties.TotalMessageCount,
            SizeInBytes = properties.SizeInBytes,
            TransferMessageCount = properties.TransferMessageCount,
            DeadLetterMessageCount = properties.DeadLetterMessageCount,
            TransferDeadLetterMessageCount = properties.TransferDeadLetterMessageCount,
            ScheduledMessageCount = properties.ScheduledMessageCount,
            AccessedAt = properties.AccessedAt,
            CreatedAt = properties.CreatedAt,
            UpdatedAt = properties.UpdatedAt
        };
    }
    public static QueueProperties ToQueueProperties(this SubscriptionRuntimeProperties properties)
    {
        return new QueueProperties(properties.SubscriptionName, QueueType.Subscription)
        {
            ActiveMessageCount = properties.ActiveMessageCount,
            TotalMessageCount = properties.TotalMessageCount,
            TransferMessageCount = properties.TransferMessageCount,
            DeadLetterMessageCount = properties.DeadLetterMessageCount,
            TransferDeadLetterMessageCount = properties.TransferDeadLetterMessageCount,
            AccessedAt = properties.AccessedAt,
            CreatedAt = properties.CreatedAt,
            UpdatedAt = properties.UpdatedAt
        };
    }
    public static QueueProperties ToQueueProperties(this TopicRuntimeProperties properties)
    {
        return new QueueProperties(properties.Name, QueueType.Topic)
        {
            SizeInBytes = properties.SizeInBytes,
            ScheduledMessageCount = properties.ScheduledMessageCount,
            AccessedAt = properties.AccessedAt,
            CreatedAt = properties.CreatedAt,
            UpdatedAt = properties.UpdatedAt
        };
    }
}
