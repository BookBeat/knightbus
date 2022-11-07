using System;
using System.Linq;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Core.Singleton;
using KnightBus.Host.MessageProcessing;
using KnightBus.Host.Singleton;
using KnightBus.Host.Tests.Unit.ExampleProcessors;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit
{

    [TestFixture]
    public class MessageProcessorLocatorTests
    {
        private Mock<ITransportChannelFactory> _queueStarterFactory;

        [SetUp]
        public void Setup()
        {
            _queueStarterFactory = new Mock<ITransportChannelFactory>();
            var transportConfiguration = new Mock<ITransportConfiguration>();
            _queueStarterFactory.Setup(x => x.Configuration).Returns(transportConfiguration.Object);
        }

        [Test]
        public void Should_locate_single_command_processor()
        {
            //arrange
            var collection = new ServiceCollection();
            collection.RegisterProcessor<SingleCommandProcessor>();
            collection.AddScoped((_) => Mock.Of<ICountable>());
            
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(collection.BuildServiceProvider())
                
            }, new[]{ _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommand))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestCommand), null, It.IsAny<TestTopicSettings>(), It.IsAny<IMessageSerializer>(), It.IsAny<HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IChannelReceiver>()).Verifiable();
            //act
            var reader = locator.CreateReceivers().ToList();
            //assert
            reader.Count.Should().Be(1);
            _queueStarterFactory.Verify();
        }
        
        [Test]
        public void Should_locate_single_request_processor()
        {
            //arrange
            var collection = new ServiceCollection();
            collection.RegisterProcessor<SingleRequestProcessor>();
            collection.AddScoped((_) => Mock.Of<ICountable>());
            
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(collection.BuildServiceProvider())
                
            }, new[]{ _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestRequest))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestRequest), null, It.IsAny<TestMessageSettings>(), It.IsAny<IMessageSerializer>(), It.IsAny<HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IChannelReceiver>()).Verifiable();
            //act
            var reader = locator.CreateReceivers().ToList();
            //assert
            reader.Count.Should().Be(1);
            _queueStarterFactory.Verify();
        }

        [Test]
        public void Should_locate_single__stream_request_processor()
        {
            //arrange
            var collection = new ServiceCollection();
            collection.RegisterProcessor<StreamRequestProcessor>();
            collection.AddScoped((_) => Mock.Of<ICountable>());
            
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(collection.BuildServiceProvider())
                
            }, new[]{ _queueStarterFactory.Object });
            
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestRequest))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestRequest), null, It.IsAny<TestMessageSettings>(), It.IsAny<IMessageSerializer>(), It.IsAny<HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IChannelReceiver>()).Verifiable();
            //act
            var reader = locator.CreateReceivers().ToList();
            //assert
            reader.Count.Should().Be(1);
            _queueStarterFactory.Verify();
        }

        [Test]
        public void Should_locate_multiple_command_processors()
        {
            //arrange
            var collection = new ServiceCollection();
            collection.RegisterProcessor<MultipleCommandProcessor>();
            collection.AddScoped((_) => Mock.Of<ICountable>());
            
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(collection.BuildServiceProvider())
                
            }, new[]{ _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommandOne))).Returns(true);
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommandTwo))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestCommandOne), null, It.IsAny<TestTopicSettings>(),It.IsAny<IMessageSerializer>(), It.IsAny<HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IChannelReceiver>()).Verifiable();
            _queueStarterFactory.Setup(x => x.Create(typeof(TestCommandTwo), null, It.IsAny<TestTopicSettings>(), It.IsAny<IMessageSerializer>(), It.IsAny<HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IChannelReceiver>()).Verifiable();
            //act
            var reader = locator.CreateReceivers().ToList();
            //assert
            reader.Count.Should().Be(2);
            _queueStarterFactory.Verify();
        }

        [Test]
        public void Should_throw_when_message_transport_is_unknown()
        {
            //arrange
            var collection = new ServiceCollection();
            collection.RegisterProcessor<SingleCommandProcessor>();
            collection.AddScoped((_) => Mock.Of<ICountable>());
            
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(collection.BuildServiceProvider())
                
            }, new[]{ _queueStarterFactory.Object });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestCommand))).Returns(false);
            //act and assert
            locator.Invoking(x => x.CreateReceivers().ToList()).Should().Throw<TransportMissingException>();
        }

        [Test]
        public void Should_locate_event_processors()
        {
            //arrange
            var collection = new ServiceCollection();
            collection.RegisterProcessor<EventProcessor>();
            collection.AddScoped((_) => Mock.Of<ICountable>());
            
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(collection.BuildServiceProvider())
                
            }, new[]{ _queueStarterFactory.Object });
            
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(TestEvent))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(TestEvent), It.IsAny<TestSubscription>(), It.IsAny<TestTopicSettings>(), It.IsAny<IMessageSerializer>(), It.IsAny<HostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(Mock.Of<IChannelReceiver>()).Verifiable();
            //act
            var reader = locator.CreateReceivers().ToList();
            //assert
            reader.Count.Should().Be(1);
            _queueStarterFactory.Verify();
        }

        [Test]
        public void Should_should_locate_singleton_processors()
        {
            //arrange
            var collection = new ServiceCollection();
            collection.RegisterProcessor<SingletonCommandProcessor>();
            collection.UseSingletonLocks(Mock.Of<ISingletonLockManager>());
            var locator = new MessageProcessorLocator(new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(collection.BuildServiceProvider())

            }, new[]{ _queueStarterFactory.Object });
            
            var underlyingQueueStarter = new Mock<IChannelReceiver>();
            underlyingQueueStarter.Setup(x => x.Settings).Returns(new SingletonProcessingSettings
            {
                MessageLockTimeout = TimeSpan.FromMinutes(1),
                DeadLetterDeliveryLimit = 1
            });
            _queueStarterFactory.Setup(x => x.CanCreate(typeof(SingletonCommand))).Returns(true);
            _queueStarterFactory.Setup(x => x.Create(typeof(SingletonCommand), null, It.IsAny<TestTopicSettings>(), It.IsAny<IMessageSerializer>(), It.IsAny<IHostConfiguration>(), It.IsAny<IMessageProcessor>()))
                .Returns(underlyingQueueStarter.Object).Verifiable();
            //act
            var reader = locator.CreateReceivers().ToList();
            //assert
            var valid = reader.Where(x => x is SingletonChannelReceiver).ToList();
            valid.Count.Should().Be(1);
        }
    }
}