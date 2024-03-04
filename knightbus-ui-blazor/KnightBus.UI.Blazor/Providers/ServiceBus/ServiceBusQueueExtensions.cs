using Azure.Messaging.ServiceBus.Administration;

namespace KnightBus.UI.Blazor.Providers.ServiceBus;

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
    public static SubscriptionQueueProperties ToQueueProperties(this SubscriptionRuntimeProperties properties, IQueueManager manager, string topic)
    {
        return new SubscriptionQueueProperties(properties.SubscriptionName, manager, topic, false)
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
        return new QueueProperties(properties.Name, manager, false, QueueType.Topic)
        {
            SizeInBytes = properties.SizeInBytes,
            ScheduledMessageCount = properties.ScheduledMessageCount,
            AccessedAt = properties.AccessedAt,
            CreatedAt = properties.CreatedAt,
            UpdatedAt = properties.UpdatedAt
        };
    }

    public static IServiceCollection AddServiceBus(this IServiceCollection services)
    {
        return services
            .AddScoped<IQueueManager, ServiceBusQueueManager>()
            .AddScoped<IQueueManager, ServiceBusTopicManager>()
            .AddScoped<ServiceBusQueueManager>()
            .AddScoped<ServiceBusTopicManager>();
    }
}
