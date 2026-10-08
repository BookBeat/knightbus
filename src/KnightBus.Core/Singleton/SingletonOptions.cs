using System;

namespace KnightBus.Core.Singleton;

/// <summary>
/// Timing of the lock that guards an <see cref="ISingletonProcessor"/>. The defaults match the
/// values used before they were configurable.
/// </summary>
public class SingletonOptions
{
    /// <summary>
    /// How often an instance that does not hold the lock tries to take it.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// A random share of <see cref="PollInterval"/>, between 0 and 1, added to each wait so that
    /// instances that started together do not poll in lock-step. The wait is never shorter than
    /// <see cref="PollInterval"/>. 0 disables the jitter.
    /// </summary>
    public double PollJitter { get; set; } = 0.2;

    /// <summary>
    /// How long a lock is held without being renewed. Must be longer than
    /// <see cref="RenewalInterval"/> with room for failed renewals.
    /// </summary>
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How often the holder renews the lock.
    /// </summary>
    public TimeSpan RenewalInterval { get; set; } = TimeSpan.FromSeconds(19);

    internal void Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(PollInterval),
                PollInterval,
                "Must be positive"
            );
        if (PollJitter < 0 || PollJitter > 1)
            throw new ArgumentOutOfRangeException(
                nameof(PollJitter),
                PollJitter,
                "Must be between 0 and 1"
            );
        if (RenewalInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(RenewalInterval),
                RenewalInterval,
                "Must be positive"
            );
        if (LockDuration <= RenewalInterval)
            throw new ArgumentException(
                $"{nameof(LockDuration)} ({LockDuration}) must be longer than {nameof(RenewalInterval)} ({RenewalInterval}), or the lock expires between renewals",
                nameof(LockDuration)
            );
    }
}
