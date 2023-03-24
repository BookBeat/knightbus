using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NATS.Client;
using NUnit.Framework;

namespace KnightBus.Nats.Tests.Unit
{
    [TestFixture]
    public class NatsBusTests
    {
        private Mock<IConnectionFactory> _factory = new Mock<IConnectionFactory>();
        private Mock<IConnection> _connection = new Mock<IConnection>();
        [SetUp]
        public void Setup()
        {
            _factory.Setup(c => c.CreateConnection(It.IsAny<Options>()))
                .Returns(_connection.Object);
        }
        [Test]
        public void When_send_should_publish_message()
        {
            //arrange

            var bus = new NatsBus(_factory.Object, new NatsConfiguration());
            //act 
            bus.Send(new TestNatsCommand());
            //assert
            _connection.Verify(c => c.Publish(It.Is<Msg>(m => m.Subject == "queueName")), Times.Once);
        }

        [Test]
        public void When_publish_should_publish_message()
        {
            //arrange
            var bus = new NatsBus(_factory.Object, new NatsConfiguration());
            //act 
            bus.Publish(new TestNatsEvent());
            //assert
            _connection.Verify(c => c.Publish(It.Is<Msg>(m => m.Subject == "topicName")), Times.Once);
        }

        [Test]
        public async Task When_request_should_publish_message_and_receive()
        {
            //arrange
            var config = new NatsConfiguration();

            _connection.Setup(x =>
                    x.RequestAsync("requestName", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Msg("reply", config.MessageSerializer.Serialize(new TestNatsResponse())));
            var bus = new NatsBus(_factory.Object, config);
            //act 
            var response = await bus.RequestAsync<TestNatsRequest, TestNatsResponse>(new TestNatsRequest());
            //assert
            response.Should().NotBeNull();
        }

        [Test]
        public void When_request_stream_should_recieve_all_messages()
        {
            //arrange
            var sub = new Mock<ISyncSubscription>();
            var stopHeader = new MsgHeader { { MsgConstants.HeaderName, MsgConstants.Completed } };
            //10 results
            sub.SetupSequence(x => x.NextMessage())
                .Returns(new Msg("inbox", Array.Empty<byte>()))
                .Returns(new Msg("inbox", stopHeader, Array.Empty<byte>()));


            _connection.Setup(x => x.SubscribeSync(It.IsAny<string>())).Returns(sub.Object);
            var bus = new NatsBus(_factory.Object, new NatsConfiguration());
            //act 
            var response = bus.RequestStream<TestNatsRequest, TestNatsResponse>(new TestNatsRequest());

            //assert
            response.Count().Should().Be(2);
        }

        [Test]
        public void When_request_stream_error_should_throw()
        {
            //arrange
            var sub = new Mock<ISyncSubscription>();
            var errorHeader = new MsgHeader { { MsgConstants.HeaderName, MsgConstants.Error } };
            //10 results
            sub.SetupSequence(x => x.NextMessage())
                .Returns(new Msg("inbox", Array.Empty<byte>()))
                .Returns(new Msg("inbox", errorHeader, Array.Empty<byte>()));


            _connection.Setup(x => x.SubscribeSync(It.IsAny<string>())).Returns(sub.Object);
            var bus = new NatsBus(_factory.Object, new NatsConfiguration());
            //act 
            var response = bus.RequestStream<TestNatsRequest, TestNatsResponse>(new TestNatsRequest());

            //assert
            response.Invoking(x => x.Count()).Should().Throw<NATSException>();
        }
    }
}
