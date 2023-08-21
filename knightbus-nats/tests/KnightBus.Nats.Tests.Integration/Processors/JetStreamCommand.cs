using KnightBus.Messages;
using KnightBus.Nats.Messages;

namespace KnightBus.Nats.Tests.Integration.Processors;

public class JetStreamCommand : IJetStreamCommand
{

}
public class JetStreamCommandMapping : IMessageMapping<JetStreamCommand>
{
    public string QueueName => "knightbus.tests.jetstreamcommand";
}
