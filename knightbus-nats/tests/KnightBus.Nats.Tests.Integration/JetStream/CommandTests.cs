using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Nats.Tests.Integration.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NATS.Client;
using NUnit.Framework;

namespace KnightBus.Nats.Tests.Integration.JetStream;

[TestFixture]
public class CommandTests
{
    [Test]
    public async Task Should_process_command_once()
    {
        var counter = TestHostSetup.ServiceProvider.GetService<ServiceReplacement<IExecutionCounter>>();
        var counterMock = new Mock<IExecutionCounter>();
        counter.Replace(counterMock.Object);
        var factory = new ConnectionFactory();
        var connection = factory.CreateConnection();
        var cmd = new JetStreamCommand("Should_process_command");
        var bus = new JetStreamBus(connection, new JetStreamConfiguration(), null);

        await bus.Send(cmd, CancellationToken.None);
        await Task.Delay(1);
        counterMock.Verify(x => x.Increment(), Times.Once);

    }
}
