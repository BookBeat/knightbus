using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Host.MessageProcessing.Processors;
using KnightBus.Host.Tests.Unit.ExampleProcessors;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit;

[TestFixture]
public class StandardMessagePipelineTests
{
    private IMessageProcessor _messageProcessor;
    private Mock<IMessageStateHandler<TestCommandOne>> _stateHandler;
    private Mock<ICountable> _countable;
    private Mock<ILogger> _logger;
    private Mock<IDependencyInjection> _messageHandlerProvider;
    private Mock<IPipelineInformation> _pipelineInformation;
    private Mock<IHostConfiguration> _hostConfiguration;


    [SetUp]
    public void Setup()
    {
        _messageHandlerProvider = new Mock<IDependencyInjection>();
        _logger = new Mock<ILogger>();
        _stateHandler = new Mock<IMessageStateHandler<TestCommandOne>>();
        _countable = new Mock<ICountable>();
        _messageHandlerProvider.Setup(x => x.GetScope()).Returns(_messageHandlerProvider.Object);
        _messageHandlerProvider.Setup(x => x.GetInstance<IProcessMessage<TestCommandOne, Task>>(typeof(MultipleCommandProcessor))).Returns(
            () => new MultipleCommandProcessor(_countable.Object)
        );
        _stateHandler.Setup(x => x.MessageScope).Returns(_messageHandlerProvider.Object);
        _hostConfiguration = new Mock<IHostConfiguration>();
        _hostConfiguration.Setup(x => x.DependencyInjection).Returns(_messageHandlerProvider.Object);
        _pipelineInformation = new Mock<IPipelineInformation>();
        _pipelineInformation.Setup(x => x.HostConfiguration).Returns(_hostConfiguration.Object);

        var middlewares = new List<IMessageProcessorMiddleware>
        {
            new ThrottlingMiddleware(1)
        };
        var pipeline = new MiddlewarePipeline(middlewares, _pipelineInformation.Object, _logger.Object);
        _messageProcessor = pipeline.GetPipeline(new MessageProcessor(typeof(MultipleCommandProcessor)));
    }

    [Test]
    public async Task Should_process_message()
    {
        //arrange
        _stateHandler.Setup(x => x.DeliveryCount).Returns(1);
        _stateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
        _stateHandler.Setup(x => x.GetMessage()).Returns(new TestCommandOne());
        //act
        await _messageProcessor.ProcessAsync(_stateHandler.Object, CancellationToken.None);
        //assert
        _stateHandler.Verify(x => x.CompleteAsync(), Times.Once);
        _countable.Verify(x => x.Count(), Times.Once);

    }
    [Test]
    public async Task Should_deadletter_message()
    {
        //arrange
        _stateHandler.Setup(x => x.GetMessage()).Returns(new TestCommandOne());
        _stateHandler.Setup(x => x.DeliveryCount).Returns(2);
        _stateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
        //act
        await _messageProcessor.ProcessAsync(_stateHandler.Object, CancellationToken.None);
        //assert
        _stateHandler.Verify(x => x.DeadLetterAsync(1), Times.Once);
        _countable.Verify(x => x.Count(), Times.Never);
    }
    [Test]
    public async Task Should_abandon_message()
    {
        //arrange
        _stateHandler.Setup(x => x.DeliveryCount).Returns(1);
        _stateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
        _stateHandler.Setup(x => x.GetMessage()).Returns(new TestCommandOne { Throw = true });
        //act
        await _messageProcessor.ProcessAsync(_stateHandler.Object, CancellationToken.None);
        //assert
        _stateHandler.Verify(x => x.AbandonByErrorAsync(It.IsAny<TestException>()), Times.Once);
    }

    [Test]
    public async Task Should_log_error()
    {
        //arrange
        _stateHandler.Setup(x => x.DeliveryCount).Returns(1);
        _stateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
        _stateHandler.Setup(x => x.GetMessage()).Returns(new TestCommandOne { Throw = true });
        //act
        await _messageProcessor.ProcessAsync(_stateHandler.Object, CancellationToken.None);
        //assert
        _logger.Verify(logger => logger.Log(
                It.Is<LogLevel>(logLevel => logLevel == LogLevel.Error),
                It.Is<EventId>(eventId => eventId.Id == 0),
                It.Is<It.IsAnyType>((@object, @type) => @object.ToString().StartsWith("Error processing message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
