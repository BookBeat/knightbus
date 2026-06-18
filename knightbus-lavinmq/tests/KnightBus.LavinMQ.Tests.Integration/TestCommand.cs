using KnightBus.LavinMQ.Messages;
using KnightBus.Messages;

namespace KnightBus.LavinMQ.Tests.Integration;

public class TestCommand : ILavinMQCommand
{
    public TestCommand() { }

    public TestCommand(string message)
    {
        Message = message;
    }

    public string Message { get; set; } = string.Empty;
}

public class TestCommandMapping : IMessageMapping<TestCommand>
{
    public string QueueName => "lavinmq-test-command";
}
