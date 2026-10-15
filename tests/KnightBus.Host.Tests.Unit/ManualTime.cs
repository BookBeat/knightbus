using System;
using System.Threading;

namespace KnightBus.Host.Tests.Unit;

/// <summary>
/// A clock that only moves when a test advances it
/// </summary>
internal sealed class ManualTime : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
