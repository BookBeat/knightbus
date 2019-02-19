using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Messages;
using Microsoft.Azure.ServiceBus;
using Moq;
using NUnit.Framework;

namespace KnightBus.Azure.ServiceBus.Unit
{
    [TestFixture]
    public class ServiceBusTests
    {
        [Test]
        public void When_SendAsync_Throws_ServiceBusCommunicationException_Should_Retry_Three_Times()
        {
            var client = new Mock<IQueueClient>();
            var clientFactory = new MockClientFactory(client.Object, Mock.Of<ITopicClient>());
            client.Setup(c => c.SendAsync(It.IsAny<Message>())).Throws(new ServiceBusCommunicationException("Some communication error"));

            var config = new Mock<IServiceBusConfiguration>();
            config.Setup(c => c.MessageSerializer.Serialize(It.IsAny<IMessage>())).Returns(string.Empty);
            var bus = new ServiceBus(config.Object) {ClientFactory = clientFactory};
            Assert.ThrowsAsync<ServiceBusCommunicationException>(async () =>
            {
                await bus.SendAsync(new MockMessage());
            });

            // One call and 3 retries
            client.Verify(c => c.SendAsync(It.IsAny<Message>()), Times.Exactly(3 + 1));
        }

        [Test]
        public void When_SendBatchAsync_Throws_ServiceBusCommunicationException_Should_Retry_Three_Times()
        {
            var client = new Mock<IQueueClient>();
            var clientFactory = new MockClientFactory(client.Object, Mock.Of<ITopicClient>());
            client.Setup(c => c.SendAsync(It.IsAny<IList<Message>>())).Throws(new ServiceBusCommunicationException("Some communication error"));

            var config = new Mock<IServiceBusConfiguration>();
            config.Setup(c => c.MessageSerializer.Serialize(It.IsAny<IMessage>())).Returns(string.Empty);
            var bus = new ServiceBus(config.Object) { ClientFactory = clientFactory };
            Assert.ThrowsAsync<ServiceBusCommunicationException>(async () =>
            {
                IList<MockMessage> messages = new List<MockMessage> {new MockMessage()};
                await bus.SendAsync(messages);
            });

            // One call and 3 retries
            client.Verify(c => c.SendAsync(It.IsAny<IList<Message>>()), Times.Exactly(3 + 1));
        }

        [Test]
        public void When_ScheduleAsync_Throws_ServiceBusCommunicationException_Should_Retry_Three_Times()
        {
            var client = new Mock<IQueueClient>();
            var clientFactory = new MockClientFactory(client.Object, Mock.Of<ITopicClient>());
            client.Setup(c => c.SendAsync(It.IsAny<Message>())).Throws(new ServiceBusCommunicationException("Some communication error"));

            var config = new Mock<IServiceBusConfiguration>();
            config.Setup(c => c.MessageSerializer.Serialize(It.IsAny<IMessage>())).Returns(string.Empty);
            var bus = new ServiceBus(config.Object) { ClientFactory = clientFactory };
            Assert.ThrowsAsync<ServiceBusCommunicationException>(async () =>
            {
                await bus.SendAsync(new MockMessage());
            });

            // One call and 3 retries
            client.Verify(c => c.SendAsync(It.IsAny<Message>()), Times.Exactly(3 + 1));
        }

        [Test]
        public void When_PublishAsync_Throws_ServiceBusCommunicationException_Should_Retry_Three_Times()
        {
            var client = new Mock<ITopicClient>();
            var clientFactory = new MockClientFactory(Mock.Of<IQueueClient>(), client.Object);
            client.Setup(c => c.SendAsync(It.IsAny<Message>())).Throws(new ServiceBusCommunicationException("Some communication error"));

            var config = new Mock<IServiceBusConfiguration>();
            config.Setup(c => c.MessageSerializer.Serialize(It.IsAny<IMessage>())).Returns(string.Empty);
            var bus = new ServiceBus(config.Object) { ClientFactory = clientFactory };
            Assert.ThrowsAsync<ServiceBusCommunicationException>(async () =>
            {
                await bus.PublishEventAsync(new MockEvent());
            });

            // One call and 3 retries
            client.Verify(c => c.SendAsync(It.IsAny<Message>()), Times.Exactly(3 + 1));
        }
    }

    public class MockClientFactory : IClientFactory
    {
        private readonly IQueueClient _queueClient;
        private readonly ITopicClient _topicClient;

        public MockClientFactory(IQueueClient queueClient, ITopicClient topicClient)
        {
            _queueClient = queueClient;
            _topicClient = topicClient;
        }

        public Task<IQueueClient> GetQueueClient<T>() where T : ICommand
        {
            return Task.FromResult(_queueClient);
        }

        public Task<ITopicClient> GetTopicClient<T>() where T : IEvent
        {
            return Task.FromResult(_topicClient);
        }

        public Task<ISubscriptionClient> GetSubscriptionClient<TTopic, TSubscription>(TSubscription subscription) where TTopic : IEvent where TSubscription : IEventSubscription<TTopic>
        {
            throw new System.NotImplementedException();
        }
    }

    internal class MockMessage : IServiceBusCommand { }

    internal class MockEvent : IServiceBusEvent { }
}


