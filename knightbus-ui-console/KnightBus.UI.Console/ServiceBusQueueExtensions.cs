using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Azure.ServiceBus;
using KnightBus.UI.Console.Providers;
using KnightBus.UI.Console.Providers.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        return new QueueProperties(properties.SubscriptionName, manager, false)
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
            UpdatedAt = properties.UpdatedAt,
        };
    }

    public static IServiceCollection UseServiceBus(this IServiceCollection collection, IConfiguration configuration)
    {
        var config = configuration.GetSection(ServiceBusConnectionConfig.SectionName).Get<ServiceBusConnectionConfig>();
        if (string.IsNullOrEmpty(config?.ConnectionString))
            return collection;

        collection.AddSingleton<IQueueManager, ServiceBusQueueManager>();
        collection.AddSingleton<IQueueManager, ServiceBusTopicManager>();
        return collection.UseServiceBus(c => c.ConnectionString = config.ConnectionString);
    }
}
