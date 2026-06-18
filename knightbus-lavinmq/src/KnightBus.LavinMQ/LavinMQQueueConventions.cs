namespace KnightBus.LavinMQ;

/// <summary>
/// Deterministic naming for the AMQP topology (queues, exchanges, dead-letter resources)
/// used by the LavinMQ transport. Naming depends only on the message queue/topic name so that
/// senders, receivers and the management client all resolve identical names without coordination.
/// </summary>
public static class LavinMQQueueConventions
{
    /// <summary>The shared delayed-message exchange used for scheduling.</summary>
    public const string DelayedExchangeName = "knightbus.delayed";

    public const string DeadLetterExchangeArgument = "x-dead-letter-exchange";
    public const string DeliveryLimitArgument = "x-delivery-limit";
    public const string DelayedTypeArgument = "x-delayed-type";
    public const string DelayedExchangeType = "x-delayed-message";
    public const string DelayHeader = "x-delay";
    public const string DeliveryCountHeader = "x-delivery-count";

    /// <summary>The per-queue dead-letter exchange that dead-lettered messages are routed to.</summary>
    public static string DeadLetterExchangeName(string queueName) => $"{queueName}.dlx";

    /// <summary>The dead-letter queue holding messages that exceeded the delivery limit or were rejected.</summary>
    public static string DeadLetterQueueName(string queueName) => $"{queueName}.dl";

    /// <summary>The queue backing a single event subscription, bound to the event's fanout exchange.</summary>
    public static string SubscriptionQueueName(string topic, string subscription) =>
        $"{topic}.{subscription}";
}
