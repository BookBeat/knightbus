using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using KnightBus.Core.DefaultMiddlewares;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class ErrorHandlingMiddlewareTests
{
    [Test]
    public void Should_catch_errors()
    {
        //arrange
        var nextProcessor = new Mock<IMessageProcessor>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        nextProcessor
            .Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
            .Throws<Exception>();
        var logger = new Mock<ILogger>();
        var middleware = new ErrorHandlingMiddleware(logger.Object);
        //act & assert
        middleware
            .Invoking(async x =>
                await x.ProcessAsync(
                    messageStateHandler.Object,
                    Mock.Of<IPipelineInformation>(),
                    nextProcessor.Object,
                    CancellationToken.None
                )
            )
            .Should()
            .NotThrowAsync();
    }

    [Test]
    public async Task Should_log_errors()
    {
        //arrange
        var nextProcessor = new Mock<IMessageProcessor>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        nextProcessor
            .Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
            .Throws<Exception>();
        var logger = new Mock<ILogger>();
        var middleware = new ErrorHandlingMiddleware(logger.Object);
        //act
        await middleware.ProcessAsync(
            messageStateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );
        //assert
        logger.Verify(
            logger =>
                logger.Log(
                    It.Is<LogLevel>(logLevel => logLevel == LogLevel.Error),
                    It.Is<EventId>(eventId => eventId.Id == 0),
                    It.Is<It.IsAnyType>(
                        (@object, @type) =>
                            @object.ToString()!.StartsWith("Error processing message")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task Should_abandon_message_on_errors()
    {
        //arrange
        var nextProcessor = new Mock<IMessageProcessor>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        nextProcessor
            .Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
            .Throws<Exception>();
        var logger = new Mock<ILogger>();
        var middleware = new ErrorHandlingMiddleware(logger.Object);
        //act
        await middleware.ProcessAsync(
            messageStateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );
        //assert
        messageStateHandler.Verify(x => x.AbandonByErrorAsync(It.IsAny<Exception>()), Times.Once);
    }

    [Test]
    public async Task Should_log_a_cancelled_handler_as_information_and_still_abandon_the_message()
    {
        //arrange: the handler is stopped by the token, like on a shutdown or a lock hand-over
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var nextProcessor = new Mock<IMessageProcessor>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        nextProcessor
            .Setup(x => x.ProcessAsync(messageStateHandler.Object, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));
        var logger = new Mock<ILogger>();
        var middleware = new ErrorHandlingMiddleware(logger.Object);
        //act
        await middleware.ProcessAsync(
            messageStateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            cts.Token
        );
        //assert
        logger.Verify(
            l =>
                l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (o, _) => o.ToString()!.StartsWith("Processing of message")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
        logger.Verify(
            l =>
                l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Never
        );
        messageStateHandler.Verify(x => x.AbandonByErrorAsync(It.IsAny<Exception>()), Times.Once);
    }

    [Test]
    public async Task Should_still_log_a_cancellation_that_did_not_come_from_the_token_as_an_error()
    {
        //arrange: for example an HttpClient timeout, which shares the exception type
        var nextProcessor = new Mock<IMessageProcessor>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        nextProcessor
            .Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
            .ThrowsAsync(new TaskCanceledException("The request timed out"));
        var logger = new Mock<ILogger>();
        var middleware = new ErrorHandlingMiddleware(logger.Object);
        //act
        await middleware.ProcessAsync(
            messageStateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );
        //assert
        logger.Verify(
            l =>
                l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Test]
    public void Should_not_throw_when_abandon_message_on_errors_fails()
    {
        //arrange
        var nextProcessor = new Mock<IMessageProcessor>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        messageStateHandler
            .Setup(x => x.AbandonByErrorAsync(It.IsAny<Exception>()))
            .Throws<Exception>();
        nextProcessor
            .Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
            .Throws<Exception>();
        var logger = new Mock<ILogger>();
        var middleware = new ErrorHandlingMiddleware(logger.Object);
        //act
        middleware
            .Invoking(async x =>
                await x.ProcessAsync(
                    messageStateHandler.Object,
                    Mock.Of<IPipelineInformation>(),
                    nextProcessor.Object,
                    CancellationToken.None
                )
            )
            .Should()
            .NotThrowAsync();
        //assert
        logger.Verify(
            logger =>
                logger.Log(
                    It.Is<LogLevel>(logLevel => logLevel == LogLevel.Error),
                    It.Is<EventId>(eventId => eventId.Id == 0),
                    It.Is<It.IsAnyType>(
                        (@object, @type) =>
                            @object.ToString()!.StartsWith("Failed to abandon message")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}
