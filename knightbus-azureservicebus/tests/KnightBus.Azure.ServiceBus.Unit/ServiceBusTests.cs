using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Messages;
using Moq;
using NUnit.Framework;

namespace KnightBus.Azure.ServiceBus.Unit
{
    [TestFixture]
    public class ServiceBusTests
    {
        [Test]
        [Parallelizable]
        public void When_SendAsync_Throws_ServiceBusCommunicationException_Should_Retry_Three_Times()
        {
            var client = new Mock<ServiceBusSender>();
            var clientFactory = new MockClientFactory(client.Object);
            client.Setup(c => c.SendMessageAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None)).Throws(new ServiceBusException("Some communication error", ServiceBusFailureReason.ServiceTimeout));

            var config = new Mock<IServiceBusConfiguration>();
            config.Setup(c => c.MessageSerializer.Serialize(It.IsAny<IMessage>())).Returns(string.Empty);
            var bus = new ServiceBus(config.Object) {ClientFactory = clientFactory};
            Assert.ThrowsAsync<ServiceBusException>(async () =>
            {
                await bus.SendAsync(new MockMessage());
            });

            // One call and 3 retries
            client.Verify(c => c.SendMessageAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None), Times.Exactly(3 + 1));
        }

        [Test]
        [Parallelizable]
        public void When_SendBatchAsync_Throws_ServiceBusCommunicationException_Should_Retry_Three_Times()
        {
            var client = new Mock<ServiceBusSender>();
            var clientFactory = new MockClientFactory(client.Object);
            client.Setup(c => c.SendMessagesAsync(It.IsAny<IList<ServiceBusMessage>>(), CancellationToken.None)).Throws(new ServiceBusException("Some communication error", ServiceBusFailureReason.ServiceTimeout));

            var config = new Mock<IServiceBusConfiguration>();
            config.Setup(c => c.MessageSerializer.Serialize(It.IsAny<IMessage>())).Returns(string.Empty);
            var bus = new ServiceBus(config.Object) { ClientFactory = clientFactory };
            Assert.ThrowsAsync<ServiceBusException>(async () =>
            {
                IList<MockMessage> messages = new List<MockMessage> {new MockMessage()};
                await bus.SendAsync(messages);
            });

            // One call and 3 retries
            client.Verify(c => c.SendMessagesAsync(It.IsAny<IList<ServiceBusMessage>>(), CancellationToken.None), Times.Exactly(3 + 1));
        }

        [Test]
        [Parallelizable]
        public void When_ScheduleAsync_Throws_ServiceBusCommunicationException_Should_Retry_Three_Times()
        {
            var client = new Mock<ServiceBusSender>();
            var clientFactory = new MockClientFactory(client.Object);
            client.Setup(c => c.SendMessageAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None)).Throws(new ServiceBusException("Some communication error", ServiceBusFailureReason.ServiceTimeout));

            var config = new Mock<IServiceBusConfiguration>();
            config.Setup(c => c.MessageSerializer.Serialize(It.IsAny<IMessage>())).Returns(string.Empty);
            var bus = new ServiceBus(config.Object) { ClientFactory = clientFactory };
            Assert.ThrowsAsync<ServiceBusException>(async () =>
            {
                await bus.ScheduleAsync(new MockMessage(), TimeSpan.Zero);
            });

            // One call and 3 retries
            client.Verify(c => c.SendMessageAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None), Times.Exactly(3 + 1));
        }

        [Test]
        [Parallelizable]
        public void When_PublishAsync_Throws_ServiceBusCommunicationException_Should_Retry_Three_Times()
        {
            var client = new Mock<ServiceBusSender>();
            var clientFactory = new MockClientFactory(client.Object);
            client.Setup(c => c.SendMessageAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None)).Throws(new ServiceBusException("Some communication error", ServiceBusFailureReason.ServiceTimeout));

            var config = new Mock<IServiceBusConfiguration>();
            config.Setup(c => c.MessageSerializer.Serialize(It.IsAny<IMessage>())).Returns(string.Empty);
            var bus = new ServiceBus(config.Object) { ClientFactory = clientFactory };
            Assert.ThrowsAsync<ServiceBusException>(async () =>
            {
                await bus.PublishEventAsync(new MockEvent());
            });

            // One call and 3 retries
            client.Verify(c => c.SendMessageAsync(It.IsAny<ServiceBusMessage>(), CancellationToken.None), Times.Exactly(3 + 1));
        }
    }

    public class MockClientFactory : IClientFactory
    {
        private readonly ServiceBusSender _queueClient;

        public MockClientFactory(ServiceBusSender queueClient)
        {
            _queueClient = queueClient;
        }

        public Task<ServiceBusSender> GetSenderClient<T>() where T : IMessage
        {
            return Task.FromResult(_queueClient);
        }

        public Task<ServiceBusProcessor> GetReceiverClient<T>(ServiceBusProcessorOptions options) where T : ICommand
        {
            throw new System.NotImplementedException();
        }

        public Task<ServiceBusProcessor> GetReceiverClient<TTopic, TSubscription>(TSubscription subscription, ServiceBusProcessorOptions options) where TTopic : IEvent where TSubscription : IEventSubscription<TTopic>
        {
            throw new System.NotImplementedException();
        }
    }

    internal class MockMessage : IServiceBusCommand { }

    internal class MockEvent : IServiceBusEvent { }
}


