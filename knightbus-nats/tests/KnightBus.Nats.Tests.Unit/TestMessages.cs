using KnightBus.Messages;
using KnightBus.Nats.Messages;

namespace KnightBus.Nats.Tests.Unit
{
    public class TestNatsCommand : INatsCommand
    {

    }

    public class TestNatsCommandMapping : IMessageMapping<TestNatsCommand>
    {
        public string QueueName => "queueName";
    }

    public class TestNatsEvent : INatsEvent
    {

    }

    public class TestNatsEventMapping : IMessageMapping<TestNatsEvent>
    {
        public string QueueName => "topicName";
    }

    public class TestNatsRequest : INatsRequest
    {

    }

    public class TestNatsResponse
    {

    }

    public class TestNatsRequestMapping : IMessageMapping<TestNatsRequest>
    {
        public string QueueName => "requestName";
    }
}
