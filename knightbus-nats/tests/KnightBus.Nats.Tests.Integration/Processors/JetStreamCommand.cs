using KnightBus.Messages;
using KnightBus.Nats.Messages;

namespace KnightBus.Nats.Tests.Integration.Processors;

public record JetStreamCommand(string Message) : IJetStreamCommand;
public class JetStreamCommandMapping : IMessageMapping<JetStreamCommand>
{
    public string QueueName => "knightbus-tests-jetstream-command42";
}

public record JetStreamEvent(string Message) : IJetStreamEvent;
public class JetStreamEventMapping : IMessageMapping<JetStreamEvent>
{
    public string QueueName => "knightbus-tests-jetstream-event2322";
}
