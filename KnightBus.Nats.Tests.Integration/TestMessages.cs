using KnightBus.Messages;
using KnightBus.Nats.Messages;

namespace KnightBus.Nats.Tests.Integration;

public class TestNatsRequest : INatsRequest
{
    public int Timeout => 100;
}

public class TestNatsResponse
{
    public string? Payload { get; init; }
}

public class TestNatsRequestMapping : IMessageMapping<TestNatsRequest>
{
    public string QueueName => "queue.name";
}