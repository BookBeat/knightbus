using System;
using System.Collections.Generic;

namespace KnightBus.Core.Singleton;

/// <summary>
/// Settings for spreading singleton locks over the hosts, see <see cref="ISingletonPlacement"/>.
/// The timing and balancing settings are internal on purpose: their defaults are part of the design
/// and are not meant to be tuned per application.
/// </summary>
public class SingletonPlacementOptions
{
    private readonly Dictionary<Type, int> _weights = new();

    /// <summary>
    /// How often a host looks at who else is a member
    /// </summary>
    internal TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The advice is dropped when the members could not be read for this long, which makes the host
    /// act as if there were no placement. Defaults to three refresh intervals.
    /// </summary>
    internal TimeSpan? StaleAfter { get; set; }

    /// <summary>
    /// How far above its fair share a host may go before the next ranked host is used, see
    /// <see cref="SingletonAssignment.Compute"/>
    /// </summary>
    internal double Epsilon { get; set; } = 0.25;

    /// <summary>
    /// How long a host must have been a member before locks are handed over to it. During a rolling
    /// deploy the group changes many times in a row, and locks should not move to a host that is
    /// about to be replaced.
    /// </summary>
    internal TimeSpan StabilityWindow { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How long a host that is not the preferred holder leaves a lock alone before it tries to take
    /// it anyway, which covers a preferred host that is alive but not running the processor
    /// </summary>
    internal TimeSpan TakeoverGrace { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The shortest time a host holds a lock before it hands it over, which keeps a lock from moving
    /// back and forth while the group is changing
    /// </summary>
    internal TimeSpan HandoffInterval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// <see cref="StaleAfter"/>, or three refresh intervals when it is not set
    /// </summary>
    internal TimeSpan EffectiveStaleAfter => StaleAfter ?? RefreshInterval * 3;

    /// <summary>
    /// Declares how heavy a singleton processor is relative to the others, so heavy ones are kept
    /// on different hosts. Processors without a declared weight count as 1.
    /// </summary>
    public SingletonPlacementOptions Weight<TProcessor>(int weight)
    {
        if (weight < 1)
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Must be at least 1");
        _weights[typeof(TProcessor)] = weight;
        return this;
    }

    /// <summary>
    /// The declared weight of a processor type, 1 when none was declared
    /// </summary>
    internal int WeightOf(Type processorType) => _weights.GetValueOrDefault(processorType, 1);

    /// <summary>
    /// Throws when the combination of settings cannot work
    /// </summary>
    internal void Validate()
    {
        if (RefreshInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(RefreshInterval),
                RefreshInterval,
                "Must be positive"
            );
        if (EffectiveStaleAfter <= RefreshInterval)
            throw new ArgumentException(
                $"{nameof(StaleAfter)} must be longer than {nameof(RefreshInterval)}, or the advice is dropped between refreshes",
                nameof(StaleAfter)
            );
        if (StabilityWindow < TimeSpan.Zero || TakeoverGrace < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(StabilityWindow), "Must not be negative");
        if (HandoffInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(HandoffInterval), "Must not be negative");
        if (Epsilon < 0)
            throw new ArgumentOutOfRangeException(nameof(Epsilon), Epsilon, "Must not be negative");
    }
}
