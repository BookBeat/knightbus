using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Nats.Tests.Integration.Processors;
using NATS.Client;
using NUnit.Framework;

namespace KnightBus.Nats.Tests.Integration.JetStream;

[TestFixture]
public class CommandTests
{
    [Test]
    public async Task Should_process_command()
    {
        var factory = new ConnectionFactory();
        var connection = factory.CreateConnection();
        var cmd = new JetStreamCommand("Should_process_command");
        var bus = new JetStreamBus(connection, new JetStreamConfiguration(), null);

        await bus.Send(cmd, CancellationToken.None);
        await Task.Delay(1);

    }
}
