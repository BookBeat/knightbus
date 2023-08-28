using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Nats.Tests.Integration.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NATS.Client;
using NUnit.Framework;

namespace KnightBus.Nats.Tests.Integration.JetStream;

public class JetStreamIntegrationTest
{
    protected IConnection Connection { get; private set; }
    protected Mock<IExecutionCounter> CounterMock { get; private set; }
    protected ExecutionCompletion Completion { get; private set; }
    protected IJetStreamBus Bus { get; private set; }


    [SetUp]
    public void Setup()
    {
        var counter = TestHostSetup.ServiceProvider.GetService<ServiceReplacement<IExecutionCounter>>();
        CounterMock = new Mock<IExecutionCounter>();
        counter.Replace(CounterMock.Object);

        var completionWrapper = TestHostSetup.ServiceProvider.GetService<ServiceReplacement<IExecutionCompletion>>();
        Completion = new ExecutionCompletion();
        completionWrapper.Replace(Completion);

        var factory = new ConnectionFactory();
        Connection = factory.CreateConnection();

        Bus = new JetStreamBus(Connection, new JetStreamConfiguration(), null);
    }


    [TearDown]
    public void TearDown()
    {
        Connection?.Close();
    }
}

[TestFixture]
public class CommandTests : JetStreamIntegrationTest
{
    [Test]
    public async Task Should_process_command_once()
    {
        //arrange
        var cmd = new JetStreamCommand("Should_process_command");

        //act
        await Bus.Send(cmd, CancellationToken.None);

        //assert
        Completion.Wait(TimeSpan.FromMinutes(1));
        CounterMock.Verify(x => x.Increment(), Times.Once);
    }
}

[TestFixture]
public class EventTests : JetStreamIntegrationTest
{
    [Test]
    public async Task Should_process_event_twice()
    {
        //arrange
        Completion.Reset(2);
        var cmd = new JetStreamEvent("Should_process_event", false);

        //act
        await Bus.Publish(cmd, CancellationToken.None).ConfigureAwait(false);

        //assert
        Completion.Wait(TimeSpan.FromSeconds(10));
        CounterMock.Verify(x => x.Increment(), Times.Exactly(2));
    }
}
