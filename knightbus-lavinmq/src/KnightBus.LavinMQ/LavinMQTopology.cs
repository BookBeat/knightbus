using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace KnightBus.LavinMQ;

/// <summary>
/// Declares the AMQP topology for the LavinMQ transport. All declarations are idempotent and use
/// deterministic names from <see cref="LavinMQQueueConventions"/>, so receivers and tests can declare
/// the exact same topology without conflicting. Dead-lettering relies on LavinMQ's native
/// <c>x-dead-letter-exchange</c> + <c>x-delivery-limit</c> arguments.
/// </summary>
public static class LavinMQTopology
{
    /// <summary>
    /// Declares a command queue together with its dead-letter exchange + queue. The queue is
    /// configured so LavinMQ dead-letters a message once it has been redelivered more than
    /// <paramref name="deadLetterDeliveryLimit"/> times.
    /// </summary>
    public static async Task DeclareCommandQueueAsync(
        IChannel channel,
        string queueName,
        int deadLetterDeliveryLimit,
        CancellationToken ct
    )
    {
        await DeclareDeadLetterAsync(channel, queueName, ct).ConfigureAwait(false);
        await channel
            .QueueDeclareAsync(
                queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: QueueArguments(queueName, deadLetterDeliveryLimit),
                cancellationToken: ct
            )
            .ConfigureAwait(false);
    }

    /// <summary>Declares the fanout exchange that an event (topic) is published to.</summary>
    public static Task DeclareEventExchangeAsync(
        IChannel channel,
        string topic,
        CancellationToken ct
    ) =>
        channel.ExchangeDeclareAsync(
            topic,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: ct
        );

    /// <summary>
    /// Declares the queue backing a single event subscription and binds it to the event's fanout
    /// exchange. Returns the name of the subscription queue to consume from.
    /// </summary>
    public static async Task<string> DeclareSubscriptionAsync(
        IChannel channel,
        string topic,
        string subscription,
        int deadLetterDeliveryLimit,
        CancellationToken ct
    )
    {
        await DeclareEventExchangeAsync(channel, topic, ct).ConfigureAwait(false);
        var queueName = LavinMQQueueConventions.SubscriptionQueueName(topic, subscription);
        await DeclareDeadLetterAsync(channel, queueName, ct).ConfigureAwait(false);
        await channel
            .QueueDeclareAsync(
                queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: QueueArguments(queueName, deadLetterDeliveryLimit),
                cancellationToken: ct
            )
            .ConfigureAwait(false);
        await channel
            .QueueBindAsync(queueName, topic, routingKey: string.Empty, cancellationToken: ct)
            .ConfigureAwait(false);
        return queueName;
    }

    /// <summary>Declares the shared delayed-message exchange used for scheduling.</summary>
    public static Task DeclareDelayedExchangeAsync(IChannel channel, CancellationToken ct) =>
        channel.ExchangeDeclareAsync(
            LavinMQQueueConventions.DelayedExchangeName,
            LavinMQQueueConventions.DelayedExchangeType,
            durable: true,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                [LavinMQQueueConventions.DelayedTypeArgument] = "direct",
            },
            cancellationToken: ct
        );

    /// <summary>Binds a command queue to the delayed-message exchange so scheduled sends are routed to it.</summary>
    public static async Task BindDelayedExchangeAsync(
        IChannel channel,
        string queueName,
        CancellationToken ct
    )
    {
        await DeclareDelayedExchangeAsync(channel, ct).ConfigureAwait(false);
        await channel
            .QueueBindAsync(
                queueName,
                LavinMQQueueConventions.DelayedExchangeName,
                routingKey: queueName,
                cancellationToken: ct
            )
            .ConfigureAwait(false);
    }

    private static async Task DeclareDeadLetterAsync(
        IChannel channel,
        string queueName,
        CancellationToken ct
    )
    {
        var deadLetterExchange = LavinMQQueueConventions.DeadLetterExchangeName(queueName);
        var deadLetterQueue = LavinMQQueueConventions.DeadLetterQueueName(queueName);
        await channel
            .ExchangeDeclareAsync(
                deadLetterExchange,
                ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                cancellationToken: ct
            )
            .ConfigureAwait(false);
        await channel
            .QueueDeclareAsync(
                deadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: ct
            )
            .ConfigureAwait(false);
        await channel
            .QueueBindAsync(
                deadLetterQueue,
                deadLetterExchange,
                routingKey: string.Empty,
                cancellationToken: ct
            )
            .ConfigureAwait(false);
    }

    private static Dictionary<string, object?> QueueArguments(
        string queueName,
        int deadLetterDeliveryLimit
    ) =>
        new()
        {
            [LavinMQQueueConventions.DeadLetterExchangeArgument] =
                LavinMQQueueConventions.DeadLetterExchangeName(queueName),
            [LavinMQQueueConventions.DeliveryLimitArgument] = deadLetterDeliveryLimit,
        };
}
