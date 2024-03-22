using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core.Management;
using Microsoft.Extensions.DependencyInjection;
using QueueProperties = KnightBus.Core.Management.QueueProperties;

namespace KnightBus.Azure.ServiceBus.Management;

public static class ServiceBusExtensions
{
    internal static QueueProperties ToQueueProperties(this QueueRuntimeProperties properties, IQueueManager manager)
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
    internal static SubscriptionQueueProperties ToQueueProperties(this SubscriptionRuntimeProperties properties, IQueueManager manager, string topic)
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

    internal static QueueProperties ToQueueProperties(this TopicRuntimeProperties properties, IQueueManager manager)
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

    public static IServiceCollection AddServiceBusManagement(this IServiceCollection services, string connectionString)
    {
        return services
            .AddScoped<IQueueManager, ServiceBusQueueManager>()
            .AddScoped<IQueueManager, ServiceBusTopicManager>()
            .AddScoped<ServiceBusQueueManager>()
            .AddScoped<ServiceBusTopicManager>()
            .UseServiceBus(c => c.ConnectionString = connectionString);
    }
}
