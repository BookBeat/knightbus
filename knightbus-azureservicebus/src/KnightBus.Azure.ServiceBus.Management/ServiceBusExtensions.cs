using System;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core.Management;
using Microsoft.Extensions.DependencyInjection;
using QueueProperties = KnightBus.Core.Management.QueueProperties;

namespace KnightBus.Azure.ServiceBus.Management;

public static class ServiceBusExtensions
{
    internal static QueueProperties ToQueueProperties(
        this QueueRuntimeProperties properties,
        IQueueManager manager
    )
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
            UpdatedAt = properties.UpdatedAt,
        };
    }

    internal static SubscriptionQueueProperties ToQueueProperties(
        this SubscriptionRuntimeProperties properties,
        IQueueManager manager,
        string topic
    )
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
            UpdatedAt = properties.UpdatedAt,
        };
    }

    internal static QueueProperties ToQueueProperties(
        this TopicRuntimeProperties properties,
        IQueueManager manager
    )
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

    private static IServiceCollection AddServiceBusManagementCore(this IServiceCollection services)
    {
        return services
            .AddScoped<IQueueMessageSender, ServiceBusQueueManager>()
            .AddScoped<IQueueManager, ServiceBusQueueManager>()
            .AddScoped<IQueueManager, ServiceBusTopicManager>()
            .AddScoped<ServiceBusQueueManager>()
            .AddScoped<ServiceBusTopicManager>();
    }

    public static IServiceCollection AddServiceBusManagement(
        this IServiceCollection services,
        string connectionString
    )
    {
        return services
            .AddServiceBusManagementCore()
            .UseServiceBus(c => c.ConnectionString = connectionString);
    }

    public static IServiceCollection AddServiceBusManagement(
        this IServiceCollection services,
        Action<IServiceBusConfiguration> configure
    )
    {
        return services.AddServiceBusManagementCore().UseServiceBus(configure);
    }

    public static IServiceCollection AddServiceBusManagement(
        this IServiceCollection services,
        IServiceBusConfiguration configuration
    )
    {
        return services.AddServiceBusManagementCore().UseServiceBus(configuration);
    }
}
