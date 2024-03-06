using System;
using KnightBus.Core;

namespace KnightBus.Nats.Tests.Integration.Processors;

public class JetStreamSettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 10;
    public int PrefetchCount => 10;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
    public int DeadLetterDeliveryLimit => 5;
}
