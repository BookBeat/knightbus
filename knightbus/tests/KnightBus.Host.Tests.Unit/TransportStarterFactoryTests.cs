using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Host.MessageProcessing.Factories;
using KnightBus.Messages;
using KnightBus.ProtobufNet;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit
{
    [TestFixture]
    public class TransportStarterFactoryTests
    {
        [Test]
        public void Should_use_default_serializer()
        {
            //arrange
            var config = new Mock<ITransportConfiguration>();
            config.Setup(x => x.MessageSerializer).Returns(new MicrosoftJsonSerializer());
            var channel = new Mock<ITransportChannelFactory>();
            channel.Setup(x => x.Middlewares).Returns(new List<IMessageProcessorMiddleware>());
            channel.Setup(x => x.CanCreate(typeof(TestCommand))).Returns(true);
            channel.Setup(x => x.Configuration).Returns(config.Object);
            var starter = new TransportStarterFactory(new[] {channel.Object}, new HostConfiguration());
            var factory = new CommandProcessorFactory();
            //act
            starter.CreateChannelReceiver(factory, typeof(IProcessCommand<TestCommand, TestMessageSettings>), typeof(JsonProcessor));
            //assert
            channel.Verify(x=> x.Create(typeof(TestCommand), null, It.IsAny<IProcessingSettings>(), It.IsAny<MicrosoftJsonSerializer>(), It.IsAny<IHostConfiguration>(), It.IsAny<IMessageProcessor>()), 
                Times.Once);
        }

        [Test]
        public void Should_use_custom_serializer()
        {
            //arrange
            var config = new Mock<ITransportConfiguration>();
            config.Setup(x => x.MessageSerializer).Returns(new MicrosoftJsonSerializer());
            var channel = new Mock<ITransportChannelFactory>();
            channel.Setup(x => x.Middlewares).Returns(new List<IMessageProcessorMiddleware>());
            channel.Setup(x => x.CanCreate(typeof(ProtobufCommand))).Returns(true);
            channel.Setup(x => x.Configuration).Returns(config.Object);
            var starter = new TransportStarterFactory(new[] {channel.Object}, new HostConfiguration());
            var factory = new CommandProcessorFactory();
            //act
            starter.CreateChannelReceiver(factory, typeof(IProcessCommand<ProtobufCommand, TestMessageSettings>), typeof(ProtobufProcessor));
            //assert
            channel.Verify(x=> x.Create(typeof(ProtobufCommand), null, It.IsAny<IProcessingSettings>(), It.IsAny<ProtobufNetSerializer>(), It.IsAny<IHostConfiguration>(), It.IsAny<IMessageProcessor>()), 
                Times.Once);
        }
    }
    
    public class JsonProcessor:IProcessCommand<TestCommand, TestMessageSettings>
    {
        public Task ProcessAsync(TestCommand message, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
    public class ProtobufProcessor:IProcessCommand<ProtobufCommand, TestMessageSettings>
    {
        public Task ProcessAsync(ProtobufCommand message, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }

    public class ProtobufCommand : ICommand
    {
    }
    
    public class ProtobufCommandMapping : IMessageMapping<ProtobufCommand>, ICustomMessageSerializer
    {
        public string QueueName => "testcommand";
        public IMessageSerializer MessageSerializer => new ProtobufNetSerializer();
    }
}