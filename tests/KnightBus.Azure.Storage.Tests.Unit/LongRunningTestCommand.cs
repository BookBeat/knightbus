using KnightBus.Azure.Storage.Messages;
using KnightBus.Messages;

namespace KnightBus.Azure.Storage.Tests.Unit;

public class LongRunningTestCommand : IStorageQueueCommand
{
    public string Message { get; set; } = null!;
}

public class LongRunningTestMessageMapping : IMessageMapping<LongRunningTestCommand>
{
    public string QueueName => "longrunningtestcommand";
}
