namespace KnightBus.Core.Singleton;

/// <summary>
/// Implement this on the <see cref="IProcessingSettings"/> of a singleton processor to say how
/// heavy it is relative to the others, so that heavy processors are kept on different hosts when
/// singleton locks are spread over the hosts. Processors without it count as 1.
/// </summary>
public interface ISingletonWeight
{
    /// <summary>
    /// The weight of the processor, at least 1
    /// </summary>
    int Weight { get; }
}
