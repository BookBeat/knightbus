using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Nats.Tests.Integration.Processors;
using Moq;
using NUnit.Framework;

namespace KnightBus.Nats.Tests.Integration.JetStream;

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
