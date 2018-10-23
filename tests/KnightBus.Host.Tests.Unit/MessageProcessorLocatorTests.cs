using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using KnightBus.Host.Singleton;
using KnightBus.Host.Tests.Unit.Processors;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit
{

    [TestFixture]
    public class MessageProcessorLocatorTests
    {
        private Mock<IMessageProcessorProvider> _messageHandlerProvider;
        private Mock<ITransportFactory> _queueStarterFactory;

        [SetUp]
        public void Setup()
        {
            _messageHandlerProvider = new Mock<IMessageProcessorProvider>();
            _queueStarterFactory = new Mock<ITransportFactory>();
            var transportConfiguration = new Mock<ITransportConfiguration>();
            transportConfiguration.Setup(x => x.Middlewares).Returns(new List<IMessageProcessorMiddleware>());
            _queueStarterFactory.Setup(x => x.Configuration).Returns(transportConfiguration.Object);
        }

        [Test]
        public void Should_locate_single_command_processor()
        {
            //arrange
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                MessageProcessorProvider = _messageHandlerProvider.Object,
                
            }, new ITransportFactory[]{ _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommand))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestCommand), null, typeof(TestTopicSettings), It.IsAny<Host.HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IStartTransport>()).Verifiable();
            _messageHandlerProvider.Setup(x => x.ListAllProcessors()).Returns(new List<Type> {typeof(SingleCommandProcessor)});
            //act
            var reader = Enumerable.ToList<IStartTransport>(locator.Locate());
            //assert
            reader.Count.Should().Be(1);
            _queueStarterFactory.Verify();
        }

        [Test]
        public void Should_locate_multiple_command_processors()
        {
            //arrange
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                MessageProcessorProvider = _messageHandlerProvider.Object,
            }, new ITransportFactory[] { _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommandOne))).Returns(true);
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommandTwo))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestCommandOne), null, typeof(TestTopicSettings), It.IsAny<Host.HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IStartTransport>()).Verifiable();
            _queueStarterFactory.Setup(x => x.Create(typeof(TestCommandTwo), null, typeof(TestTopicSettings), It.IsAny<Host.HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IStartTransport>()).Verifiable();
            _messageHandlerProvider.Setup(x => x.ListAllProcessors()).Returns(new List<Type> { typeof(MultipleCommandProcessor) });
            //act
            var reader = Enumerable.ToList<IStartTransport>(locator.Locate());
            //assert
            reader.Count.Should().Be(2);
            _queueStarterFactory.Verify();
        }

        [Test]
        public void Should_throw_when_message_transport_is_unknown()
        {
            //arrange
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                MessageProcessorProvider = _messageHandlerProvider.Object,
            }, new ITransportFactory[] { _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommand))).Returns(false);
            _messageHandlerProvider.Setup(x => x.ListAllProcessors()).Returns(new List<Type> { typeof(SingleCommandProcessor) });
            //act and assert
            AssertionExtensions.Invoking<Host.MessageProcessorLocator>(locator, x => Enumerable.ToList<IStartTransport>(x.Locate())).Should().Throw<Host.TransportMissingException>();
        }

        [Test]
        public void Should_locate_event_processors()
        {
            //arrange
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                MessageProcessorProvider = _messageHandlerProvider.Object,
            }, new ITransportFactory[] { _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestEvent))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestEvent), typeof(TestSubscription), typeof(TestTopicSettings), It.IsAny<Host.HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IStartTransport>()).Verifiable();
            _messageHandlerProvider.Setup(x => x.ListAllProcessors()).Returns(new List<Type> { typeof(EventProcessor) });
            //act
            var reader = Enumerable.ToList<IStartTransport>(locator.Locate());
            //assert
            reader.Count.Should().Be(1);
            _queueStarterFactory.Verify();
        }

        [Test]
        public void Should_should_locate_singleton_processors()
        {
            //arrange
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                MessageProcessorProvider = _messageHandlerProvider.Object,
                SingletonLockManager = Mock.Of<ISingletonLockManager>()
            }, new ITransportFactory[] { _queueStarterFactory.Object });
            var underlyingQueueStarter = new Mock<IStartTransport>();
            underlyingQueueStarter.Setup(x => x.Settings).Returns(new SingletonProcessingSettings
            {
                MessageLockTimeout = TimeSpan.FromMinutes(1),
                DeadLetterDeliveryLimit = 1
            });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(SingletonCommand))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(SingletonCommand), null, typeof(TestTopicSettings), It.IsAny<IHostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(underlyingQueueStarter.Object).Verifiable();
            _messageHandlerProvider.Setup(x => x.ListAllProcessors()).Returns(new List<Type> { typeof(SingletonCommandProcessor) });
            //act
            var reader = Enumerable.ToList<IStartTransport>(locator.Locate());
            //assert
            var valid = reader.Where(x => x is SingletonTransportStarter).ToList();
            valid.Count.Should().Be(1);
        }
    }
}