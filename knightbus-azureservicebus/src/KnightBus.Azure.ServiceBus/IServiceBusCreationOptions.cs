namespace KnightBus.Azure.ServiceBus;

public interface IServiceBusCreationOptions
{
    /// <summary>
    /// Create the queue/topic as partitioned
    /// </summary>
    /// <remarks>Overrides default value (false) from ServiceBusCreationOptions</remarks>
    bool EnablePartitioning { get; }

    /// <summary>
    /// Defines whether ordering needs to be maintained. If true, messages sent to topic will be 
    /// forwarded to the subscription in order. 
    /// </summary>
    /// <remarks>Overrides default value (false) from ServiceBusCreationOptions</remarks>
    bool SupportOrdering { get; }

    /// <summary>
    /// Indicates whether server-side batched operations are enabled.
    /// </summary>
    /// <remarks>Overrides default value (true) from ServiceBusCreationOptions</remarks>
    bool EnableBatchedOperations { get; }
}
