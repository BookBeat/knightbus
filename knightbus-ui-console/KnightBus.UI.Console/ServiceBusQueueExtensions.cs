using Azure.Messaging.ServiceBus.Administration;
using KnightBus.UI.Console.Providers;

namespace KnightBus.UI.Console;

public static class ServiceBusQueueExtensions
{
    public static QueueProperties ToQueueProperties(this QueueRuntimeProperties properties, IQueueManager manager)
    {
        return new QueueProperties(properties.Name, manager, true)
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
    public static QueueProperties ToQueueProperties(this SubscriptionRuntimeProperties properties, IQueueManager manager)
    {
        return new QueueProperties(properties.SubscriptionName, manager, true)
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
    public static QueueProperties ToQueueProperties(this TopicRuntimeProperties properties, IQueueManager manager)
    {
        return new QueueProperties(properties.Name, manager, true)
        {
            SizeInBytes = properties.SizeInBytes,
            ScheduledMessageCount = properties.ScheduledMessageCount,
            AccessedAt = properties.AccessedAt,
            CreatedAt = properties.CreatedAt,
            UpdatedAt = properties.UpdatedAt,
        };
    }
}
