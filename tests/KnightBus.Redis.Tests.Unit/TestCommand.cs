using KnightBus.Messages;
using KnightBus.Redis.Messages;

namespace KnightBus.Redis.Tests.Unit;

public class TestCommand : IRedisCommand
{
    public string Body { get; set; }

    public TestCommand(string body)
    {
        Body = body;
    }
}

public class TestCommandMapping : IMessageMapping<TestCommand>
{
    public string QueueName => "test";
}
