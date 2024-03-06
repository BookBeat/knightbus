using KnightBus.Messages;
using KnightBus.Nats.Messages;

namespace KnightBus.Nats.Tests.Integration.Processors;

public record JetStreamEvent(string Message, bool Throw) : IJetStreamEvent;

public class JetStreamEventMapping : IMessageMapping<JetStreamEvent>
{
    public string QueueName => "knightbus-tests-jetstream-evt.topic";
}
