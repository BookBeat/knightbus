using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;

namespace KnightBus.Azure.ServiceBus;

internal static class SubscriptionForwarding
{
    /// <summary>
    /// The queue the subscription forwards to, or null when it is read directly.
    /// </summary>
    public static string? GetForwardingQueue(
        IServiceBusConfiguration configuration,
        string topic,
        IEventSubscription subscription
    )
    {
        var forwarding = subscription as IServiceBusForwardingSubscription;
        var enabled = forwarding?.ForwardToQueue ?? configuration.SubscriptionForwarding.Enabled;
        if (!enabled)
            return null;
        var queueName = forwarding?.QueueName;
        return string.IsNullOrWhiteSpace(queueName) ? $"{topic}.{subscription.Name}" : queueName;
    }

    /// <summary>
    /// Makes sure the queue exists and the subscription forwards to it, creating the topic and
    /// subscription when they are missing and pointing an existing subscription at the queue.
    /// </summary>
    public static async Task EnsureAsync(
        ServiceBusAdministrationClient admin,
        string topic,
        string subscription,
        string queue,
        IServiceBusCreationOptions creationOptions,
        ILogger log,
        CancellationToken cancellationToken
    )
    {
        // The queue has to exist before a subscription can forward to it
        if (!await admin.QueueExistsAsync(queue, cancellationToken).ConfigureAwait(false))
        {
            await IgnoreAlreadyExists(
                    admin.CreateQueueAsync(
                        new CreateQueueOptions(queue)
                        {
                            EnablePartitioning = creationOptions.EnablePartitioning,
                            EnableBatchedOperations = creationOptions.EnableBatchedOperations,
                        },
                        cancellationToken
                    )
                )
                .ConfigureAwait(false);
            log.LogInformation(
                "Created queue {QueueName} for subscription {TopicName} {SubscriptionName}",
                queue,
                topic,
                subscription
            );
        }

        SubscriptionProperties? existing = null;
        try
        {
            existing = (
                await admin
                    .GetSubscriptionAsync(topic, subscription, cancellationToken)
                    .ConfigureAwait(false)
            ).Value;
        }
        catch (ServiceBusException e)
            when (e.Reason == ServiceBusFailureReason.MessagingEntityNotFound) { }

        if (existing is null)
        {
            await IgnoreAlreadyExists(
                    admin.CreateTopicAsync(
                        new CreateTopicOptions(topic)
                        {
                            EnablePartitioning = creationOptions.EnablePartitioning,
                            EnableBatchedOperations = creationOptions.EnableBatchedOperations,
                            SupportOrdering = creationOptions.SupportOrdering,
                        },
                        cancellationToken
                    )
                )
                .ConfigureAwait(false);
            await IgnoreAlreadyExists(
                    admin.CreateSubscriptionAsync(
                        new CreateSubscriptionOptions(topic, subscription)
                        {
                            EnableBatchedOperations = creationOptions.EnableBatchedOperations,
                            ForwardTo = queue,
                        },
                        cancellationToken
                    )
                )
                .ConfigureAwait(false);
            // Another instance may have created it without forwarding in between
            existing = (
                await admin
                    .GetSubscriptionAsync(topic, subscription, cancellationToken)
                    .ConfigureAwait(false)
            ).Value;
        }

        var current = ServiceBusEntityName.FromForwardTo(existing.ForwardTo);
        if (ServiceBusEntityName.AreSame(current, queue))
            return;

        existing.ForwardTo = queue;
        await admin.UpdateSubscriptionAsync(existing, cancellationToken).ConfigureAwait(false);
        log.LogInformation(
            "Subscription {TopicName} {SubscriptionName} now forwards to queue {QueueName}, previously {PreviousForwardTo}",
            topic,
            subscription,
            queue,
            current ?? "none"
        );
    }

    private static async Task IgnoreAlreadyExists<T>(Task<T> create)
    {
        try
        {
            await create.ConfigureAwait(false);
        }
        catch (ServiceBusException e)
            when (e.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists) { }
    }
}
