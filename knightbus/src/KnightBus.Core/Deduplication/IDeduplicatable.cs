#nullable enable
using System;

namespace KnightBus.Core.Deduplication;

public interface IDeduplicatable
{
    string DeduplicationKey { get; }

    /// <summary>
    /// null = outbox mode: key is released after the message has been processed.
    /// non-null = time-window mode: key expires automatically via store TTL.
    /// </summary>
    TimeSpan? DeduplicationWindow { get; }
}
