using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Host.MessageProcessing.Processors;
using KnightBus.Host.Tests.Unit.ExampleProcessors;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit.MessageProcessing.Processors;

[TestFixture]
public class MessageProcessorTests
{
    [Test]
    public async Task Should_process_message()
    {
        //arrange
        var message = new TestCommand();
        var messageProcessor = new MessageProcessor(typeof(SingleCommandProcessor));
        var commandHandler = new Mock<IProcessMessage<TestCommand, Task>>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        messageStateHandler
            .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestCommand, Task>>(typeof(SingleCommandProcessor)))
            .Returns(commandHandler.Object);
        messageStateHandler.Setup(x => x.GetMessage()).Returns(message);
        //act
        await messageProcessor.ProcessAsync(messageStateHandler.Object, CancellationToken.None);
        //assert
        commandHandler.Verify(x => x.ProcessAsync(message, CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task Should_complete_successful_message()
    {
        //arrange
        var message = new TestCommand();
        var messageProcessor = new MessageProcessor(typeof(SingleCommandProcessor));
        var commandHandler = new Mock<IProcessMessage<TestCommand, Task>>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        messageStateHandler
            .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestCommand, Task>>(typeof(SingleCommandProcessor)))
            .Returns(commandHandler.Object);
        messageStateHandler.Setup(x => x.GetMessage()).Returns(message);
        //act
        await messageProcessor.ProcessAsync(messageStateHandler.Object, CancellationToken.None);
        //assert
        messageStateHandler.Verify(x => x.CompleteAsync(), Times.Once);
    }

    [Test]
    public void Should_not_complete_failed_message()
    {
        //arrange
        var message = new TestCommand();
        var messageProcessor = new MessageProcessor(typeof(SingleCommandProcessor));
        var commandHandler = new Mock<IProcessMessage<TestCommand, Task>>();
        commandHandler.Setup(x => x.ProcessAsync(message, CancellationToken.None))
            .Throws(new Exception());
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        messageStateHandler
            .Setup(x => x.MessageScope.GetInstance<IProcessMessage<TestCommand, Task>>(typeof(SingleCommandProcessor)))
            .Returns(commandHandler.Object);
        messageStateHandler.Setup(x => x.GetMessage()).Returns(message);
        //act
        messageProcessor.Awaiting(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
            .Should().ThrowAsync<Exception>();
        //assert
        messageStateHandler.Verify(x => x.CompleteAsync(), Times.Never);
    }
}
