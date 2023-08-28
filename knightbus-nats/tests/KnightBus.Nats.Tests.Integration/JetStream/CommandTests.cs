using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Nats.Tests.Integration.Processors;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NATS.Client;
using NUnit.Framework;

namespace KnightBus.Nats.Tests.Integration.JetStream;

public class JetStreamIntegrationTest
{
    private readonly MicrosoftJsonSerializer _serializer = new MicrosoftJsonSerializer();
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

    protected T GetDeadletterMessage<T>(T message)
    {
        var queueName = AutoMessageMapper.GetQueueName(message.GetType());
        var sub = Connection.SubscribeSync(JetStreamHelpers.GetRootDeadletterSubject(queueName));
        var msg = sub.NextMessage(1000);
        return _serializer.Deserialize<T>(msg.Data.AsSpan());
    }
}

[TestFixture]
public class CommandTests : JetStreamIntegrationTest
{
    [Test]
    public async Task Should_process_command_once()
    {
        //arrange
        var cmd = new JetStreamCommand("Should_process_command", false);

        //act
        await Bus.Send(cmd, CancellationToken.None);

        //assert
        Completion.Wait(TimeSpan.FromMinutes(1));
        CounterMock.Verify(x => x.Increment(), Times.Once);
    }

    [Test]
    public async Task Should_deadletter_command_after_5_failures()
    {
        //arrange
        Completion.Reset(5);
        var cmd = new JetStreamCommand(Guid.NewGuid().ToString("N"), true);

        //act
        await Bus.Send(cmd, CancellationToken.None);
        //assert
        Completion.Wait(TimeSpan.FromSeconds(10));
        CounterMock.Verify(x => x.Increment(), Times.Never);
        var deadletter = GetDeadletterMessage(cmd);
        deadletter.Message.Should().Be(cmd.Message);
    }
}
