using System;

namespace KnightBus.Core;

/// <summary>
/// Configures settings per implementation of QueueListener
/// Exact behavior is dependent on the transport
/// </summary>
public interface IProcessingSettings
{
    /// <summary>Gets the maximum number of concurrent calls to the callback the message pump should initiate.</summary>
    /// <value>The maximum number of concurrent calls to the callback.</value>
    int MaxConcurrentCalls { get; }

    /// <summary>
    /// Gets the number of messages the message pump should pre load.
    /// </summary>
    int PrefetchCount { get; }

    /// <summary>Gets the maximum duration within which the lock will be held. </summary>
    /// <value>The maximum duration during which locks are held.</value>
    TimeSpan MessageLockTimeout { get; }

    /// <summary>
    /// Gets the maximum number of times a message can be delivered before dead lettered.
    /// This value must be lower than the queues default dead letter settings if it should have any effect. </summary>
    int DeadLetterDeliveryLimit { get; }
}

/// <summary>
/// Implement this for <see cref="IProcessingSettings"/> to be able to extend locks
/// </summary>
public interface IExtendMessageLockTimeout
{
    /// <summary>
    /// How long should the message lock be extended for
    /// </summary>
    TimeSpan ExtensionDuration { get; }

    /// <summary>
    /// How often should we renew the lock
    /// </summary>
    TimeSpan ExtensionInterval { get; }
}

/// <summary>
/// Implement this for <see cref="IProcessingSettings"/> to be able to delay re-processing
///
/// <para>
/// <b>Supported message brokers</b><br/>
///   • Azure Storage Bus <br/>
///   • PostgreSQL <br/>
/// </para>
/// Other transports will ignore the <see cref="BackOffGenerator"/>.
/// </summary>
public interface IRetryBackoff
{
    /// <summary>
    /// Delegate that computes how long the current message should stay invisible
    /// before the next re-processing attempt.
    /// </summary>
    /// <value>
    /// The function receives a <see cref="DelayBackOffGeneratorData"/> instance and must return a
    /// <see cref="TimeSpan"/> representing the delay.
    /// Return <see cref="TimeSpan.Zero"/> to retry immediately.
    /// </value>
    Func<DelayBackOffGeneratorData, TimeSpan> BackOffGenerator { get; }
}

/// <summary>
/// Context passed to <see cref="IRetryBackoff.BackOffGenerator"/> so it can
/// choose an appropriate delay. Additional fields can be added in the future
/// without breaking callers.
/// </summary>
public record DelayBackOffGeneratorData
{
    /// <summary>
    /// 1-based count of how many times the message has already been delivered
    /// to a processor (including the current attempt).
    /// For example, the first failure yields <c>DeliveryCount = 1</c>.
    /// </summary>
    public int DeliveryCount { get; init; }
}
