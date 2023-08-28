using KnightBus.Messages;
using KnightBus.Nats.Messages;

namespace KnightBus.Nats.Tests.Integration.Processors;

public record JetStreamCommand(string Message, bool Throw) : IJetStreamCommand;
public class JetStreamCommandMapping : IMessageMapping<JetStreamCommand>
{
    public string QueueName => "knightbus-tests-jetstream-command.subject";
}
