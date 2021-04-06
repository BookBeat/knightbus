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
        private StandardDependecyInjection _messageHandlerProvider;
        private Mock<ITransportChannelFactory> _queueStarterFactory;

        [SetUp]
        public void Setup()
        {
            _messageHandlerProvider = new StandardDependecyInjection();
            _queueStarterFactory = new Mock<ITransportChannelFactory>();
            var transportConfiguration = new Mock<ITransportConfiguration>();
            _queueStarterFactory.Setup(x => x.Middlewares).Returns(new List<IMessageProcessorMiddleware>());
            _queueStarterFactory.Setup(x => x.Configuration).Returns(transportConfiguration.Object);
        }

        [Test]
        public void Should_locate_single_command_processor()
        {
            //arrange
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                DependencyInjection = _messageHandlerProvider
                
            }, new[]{ _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommand))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestCommand), null, It.IsAny<TestTopicSettings>(), It.IsAny<IMessageSerializer>(), It.IsAny<HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IChannelReceiver>()).Verifiable();
            _messageHandlerProvider.RegisterProcessor(new SingleCommandProcessor(Mock.Of<ICountable>()));
            //act
            var reader = Enumerable.ToList(locator.Locate());
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
                DependencyInjection = _messageHandlerProvider,
            }, new[] { _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommandOne))).Returns(true);
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommandTwo))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestCommandOne), null, It.IsAny<TestTopicSettings>(),It.IsAny<IMessageSerializer>(), It.IsAny<HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IChannelReceiver>()).Verifiable();
            _queueStarterFactory.Setup(x => x.Create(typeof(TestCommandTwo), null, It.IsAny<TestTopicSettings>(), It.IsAny<IMessageSerializer>(), It.IsAny<HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IChannelReceiver>()).Verifiable();
            _messageHandlerProvider.RegisterProcessor(new MultipleCommandProcessor(Mock.Of<ICountable>()));
            //act
            var reader = Enumerable.ToList(locator.Locate());
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
                DependencyInjection = _messageHandlerProvider,
            }, new[] { _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommand))).Returns(false);
            _messageHandlerProvider.RegisterProcessor(new SingleCommandProcessor(Mock.Of<ICountable>()));
            //act and assert
            AssertionExtensions.Invoking(locator, x => Enumerable.ToList(x.Locate())).Should().Throw<TransportMissingException>();
        }

        [Test]
        public void Should_locate_event_processors()
        {
            //arrange
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                DependencyInjection = _messageHandlerProvider,
            }, new[] { _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestEvent))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestEvent), It.IsAny<TestSubscription>(), It.IsAny<TestTopicSettings>(), It.IsAny<IMessageSerializer>(), It.IsAny<HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IChannelReceiver>()).Verifiable();
            _messageHandlerProvider.RegisterProcessor(new EventProcessor(Mock.Of<ICountable>()));
            //act
            var reader = Enumerable.ToList(locator.Locate());
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
                DependencyInjection = _messageHandlerProvider,
                SingletonLockManager = Mock.Of<ISingletonLockManager>()
            }, new[] { _queueStarterFactory.Object });
            var underlyingQueueStarter = new Mock<IChannelReceiver>();
            underlyingQueueStarter.Setup(x => x.Settings).Returns(new SingletonProcessingSettings
            {
                MessageLockTimeout = TimeSpan.FromMinutes(1),
                DeadLetterDeliveryLimit = 1
            });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(SingletonCommand))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(SingletonCommand), null, It.IsAny<TestTopicSettings>(), It.IsAny<IMessageSerializer>(), It.IsAny<IHostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(underlyingQueueStarter.Object).Verifiable();
            _messageHandlerProvider.RegisterProcessor(new SingletonCommandProcessor());
            //act
            var reader = Enumerable.ToList(locator.Locate());
            //assert
            var valid = reader.Where(x => x is SingletonChannelReceiver).ToList();
            valid.Count.Should().Be(1);
        }
    }
}