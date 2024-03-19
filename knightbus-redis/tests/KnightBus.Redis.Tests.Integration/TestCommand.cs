using KnightBus.Messages;
using KnightBus.Redis.Messages;

namespace KnightBus.Redis.Tests.Integration;

public class TestCommand : IRedisCommand
{
    public string Value { get; set; }

    public TestCommand(string value)
    {
        Value = value;
    }
}

public class TestCommandMapping : IMessageMapping<TestCommand>
{
    public string QueueName => "test";
}
