using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.DefaultMiddlewares;
using KnightBus.Messages;
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
                            @object.ToString().StartsWith("Error processing message")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
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
        messageStateHandler.Verify(
            x => x.AbandonByErrorAsync(It.IsAny<Exception>(), TimeSpan.Zero),
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
            .Setup(x => x.AbandonByErrorAsync(It.IsAny<Exception>(), TimeSpan.Zero))
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
                            @object.ToString().StartsWith("Failed to abandon message")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()
                ),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_When_Flat_Delay_Setting_Should_Abandon_Message_With_Delay()
    {
        // Arrange
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();

        var nextProcessor = new Mock<IMessageProcessor>();
        nextProcessor
            .Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
            .Throws<Exception>();

        var pipeline = new MyPipeline { ProcessingSettings = new MyFlatDelaySetting() };

        var middleware = new ErrorHandlingMiddleware(Mock.Of<ILogger>());

        // Act
        await middleware.ProcessAsync(
            messageStateHandler.Object,
            pipeline,
            nextProcessor.Object,
            CancellationToken.None
        );

        // Assert
        messageStateHandler.Verify(
            x => x.AbandonByErrorAsync(It.IsAny<Exception>(), TimeSpan.FromMinutes(10)),
            Times.Once
        );
    }

    [Test]
    public async Task ProcessAsync_When_Exponential_Delay_Setting_Should_Abandon_Message_With_Delay()
    {
        // Arrange
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        messageStateHandler.Setup(x => x.DeliveryCount).Returns(2);

        var nextProcessor = new Mock<IMessageProcessor>();
        nextProcessor
            .Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
            .Throws<Exception>();

        var pipeline = new MyPipeline { ProcessingSettings = new MyExponentialDelaySetting() };

        var middleware = new ErrorHandlingMiddleware(Mock.Of<ILogger>());

        // Act
        await middleware.ProcessAsync(
            messageStateHandler.Object,
            pipeline,
            nextProcessor.Object,
            CancellationToken.None
        );

        // Assert
        messageStateHandler.Verify(
            x => x.AbandonByErrorAsync(It.IsAny<Exception>(), TimeSpan.FromMinutes(4)),
            Times.Once
        );
    }
}

public class MyPipeline : IPipelineInformation
{
    public Type ProcessorInterfaceType { get; }
    public IEventSubscription Subscription { get; }
    public IProcessingSettings ProcessingSettings { get; set; }
    public IHostConfiguration HostConfiguration { get; set; }
}

public abstract class MyDelaySetting : IProcessingSettings, IRetryBackoff
{
    public int MaxConcurrentCalls { get; }
    public int PrefetchCount { get; }
    public TimeSpan MessageLockTimeout { get; }
    public int DeadLetterDeliveryLimit { get; }
    public abstract Func<DelayBackOffGeneratorData, TimeSpan> BackOffGenerator { get; }
}

public class MyFlatDelaySetting : MyDelaySetting
{
    public override Func<DelayBackOffGeneratorData, TimeSpan> BackOffGenerator =>
        _ => TimeSpan.FromMinutes(10);
}

public class MyExponentialDelaySetting : MyDelaySetting
{
    public override Func<DelayBackOffGeneratorData, TimeSpan> BackOffGenerator =>
        data => TimeSpan.FromMinutes(Math.Pow(2, data.DeliveryCount));
}
