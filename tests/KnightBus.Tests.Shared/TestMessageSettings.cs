using System;
using KnightBus.Core;

namespace KnightBus.Tests.Shared;

/// <summary>
/// One message at a time, a one minute message lock and no retries, unless a test says otherwise
/// </summary>
public class TestMessageSettings : IProcessingSettings
{
    public int MaxConcurrentCalls { get; set; } = 1;
    public int PrefetchCount { get; set; }
    public TimeSpan MessageLockTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public int DeadLetterDeliveryLimit { get; set; } = 1;
}
