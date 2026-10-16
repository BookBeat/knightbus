using System;
using System.Threading;

namespace KnightBus.Tests.Shared;

/// <summary>
/// A clock that only moves when a test advances it. The wall clock starts at the real time, like a
/// storage service's clock.
/// </summary>
public sealed class ManualTime : TimeProvider
{
    private readonly DateTimeOffset _start = DateTimeOffset.UtcNow;
    private long _ticks;

    public override DateTimeOffset GetUtcNow() => _start.AddTicks(Interlocked.Read(ref _ticks));

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
