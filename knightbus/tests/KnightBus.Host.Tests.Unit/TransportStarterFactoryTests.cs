using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host.MessageProcessing.Factories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

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
        channel.Setup(x => x.CanCreate(typeof(TestCommand))).Returns(true);
        channel.Setup(x => x.Configuration).Returns(config.Object);
        var starter = new TransportStarterFactory(
            new[] { channel.Object },
            new HostConfiguration
            {
                DependencyInjection = new MicrosoftDependencyInjection(
                    new ServiceCollection().BuildServiceProvider()
                ),
            }
        );
        var factory = new CommandProcessorFactory();
        //act
        starter.CreateChannelReceiver(
            factory,
            typeof(IProcessCommand<TestCommand, TestMessageSettings>),
            typeof(JsonProcessor)
        );
        //assert
        channel.Verify(
            x =>
                x.Create(
                    typeof(TestCommand),
                    null,
                    It.IsAny<IProcessingSettings>(),
                    It.IsAny<MicrosoftJsonSerializer>(),
                    It.IsAny<IHostConfiguration>(),
                    It.IsAny<IMessageProcessor>()
                ),
            Times.Once
        );
    }
}

public class JsonProcessor : IProcessCommand<TestCommand, TestMessageSettings>
{
    public Task ProcessAsync(TestCommand message, CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }
}
