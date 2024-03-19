using KnightBus.Messages;

namespace KnightBus.Core.Tests.Unit;

public class TestCommand : ICommand { }
public class TestCommandMapping : IMessageMapping<TestCommand>
{
    public string QueueName => "test-command-queue";
}
