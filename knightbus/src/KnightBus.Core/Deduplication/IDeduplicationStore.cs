using System;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Deduplication;

public interface IDeduplicationStore
{
    /// <summary>
    /// Tries to claim a deduplication key. Returns true if the key was newly claimed, false if it already existed.
    /// </summary>
    /// <param name="deduplicationKey">The fully-qualified deduplication key.</param>
    /// <param name="ttl">Time-to-live for the key. null means the key must be released explicitly (outbox mode).</param>
    Task<bool> TryClaimAsync(
        string deduplicationKey,
        TimeSpan? ttl,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Releases a previously claimed key. Used in outbox mode after successful message processing.
    /// </summary>
    Task ReleaseAsync(string deduplicationKey, CancellationToken cancellationToken);
}
