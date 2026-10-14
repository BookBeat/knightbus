using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Core;
using KnightBus.Host.Singleton;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

[TestFixture]
public class InFlightMessagesTests
{
    [Test]
    public async Task Should_abandon_a_message_that_arrives_after_the_receiver_was_cancelled()
    {
        //arrange: the transport handed the message over just as a hand-over cancelled the receiver
        var next = new Mock<IMessageProcessor>();
        var inFlight = new InFlightMessages(next.Object);
        var message = new Mock<IMessageStateHandler<TestCommand>>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        //act
        await inFlight.ProcessAsync(message.Object, cancelled.Token);

        //assert: it never runs, and goes back to the queue
        next.Verify(
            x =>
                x.ProcessAsync(
                    It.IsAny<IMessageStateHandler<TestCommand>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        message.Verify(x => x.AbandonByErrorAsync(It.IsAny<OperationCanceledException>()));
        inFlight.Count.Should().Be(0);
    }
}
