using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Messages;
using KnightBus.Nats.Messages;
using Moq;
using NATS.Client;
using Newtonsoft.Json;
using NUnit.Framework;

namespace KnightBus.Nats.Tests.Unit
{
    [TestFixture]
    public class NatsBusTests
    {
        [Test]
        public void When_send_should_publish_message()
        {
            //arrange
            var connection = new Mock<IConnection>();
            var bus = new NatsBus(connection.Object, new NatsBusConfiguration(""));
            //act 
            bus.Send(new TestNatsCommand());
            //assert
            connection.Verify(c=> c.Publish(It.Is<Msg>( m=> m.Subject == "queueName")), Times.Once);
        }

        [Test]
        public void When_publish_should_publish_message()
        {
            //arrange
            var connection = new Mock<IConnection>();
            var bus = new NatsBus(connection.Object, new NatsBusConfiguration(""));
            //act 
            bus.Publish(new TestNatsEvent());
            //assert
            connection.Verify(c => c.Publish(It.Is<Msg>(m => m.Subject == "topicName")), Times.Once);
        }

        [Test]
        public async Task When_request_should_publish_message_and_receive()
        {
            //arrange
            var config = new NatsBusConfiguration("");
            var connection = new Mock<IConnection>();
            connection.Setup(x =>
                    x.RequestAsync("requestName", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Msg("reply", config.MessageSerializer.Serialize(new TestNatsResponse())));
            var bus = new NatsBus(connection.Object,config );
            //act 
            var response = await bus.RequestAsync<TestNatsRequest, TestNatsResponse>(new TestNatsRequest());
            //assert
            response.Should().NotBeNull();
        }
    }

    public class TestNatsCommand : INatsCommand
    {

    }

    public class TestNatsCommandMapping:IMessageMapping<TestNatsCommand>
    {
        public string QueueName => "queueName";
    }

    public class TestNatsEvent : INatsEvent
    {

    }

    public class TestNatsEventMapping : IMessageMapping<TestNatsEvent>
    {
        public string QueueName => "topicName";
    }

    public class TestNatsRequest : INatsRequest
    {

    }

    public class TestNatsResponse
    {

    }

    public class TestNatsRequestMapping : IMessageMapping<TestNatsRequest>
    {
        public string QueueName => "requestName";
    }


}