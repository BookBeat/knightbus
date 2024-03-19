using System;
using KnightBus.Azure.Storage.Messages;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage.Tests.Unit;

public class LongRunningTestCommand : IStorageQueueCommand
{
    public string Message { get; set; }
}
public class LongRunningTestMessageMapping : IMessageMapping<LongRunningTestCommand>
{
    public string QueueName => "longrunningtestcommand";
}
public class TestMessageSettings : IProcessingSettings
{
    public int MaxConcurrentCalls { get; set; } = 1;
    public TimeSpan MessageLockTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public int DeadLetterDeliveryLimit { get; set; } = 1;
    public int PrefetchCount { get; set; }
}
