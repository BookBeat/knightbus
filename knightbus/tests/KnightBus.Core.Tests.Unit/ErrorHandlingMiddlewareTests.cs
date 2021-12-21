using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.DefaultMiddlewares;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
    [TestFixture]
    public class ErrorHandlingMiddlewareTests
    {
        [Test]
        public void Should_catch_errors()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            nextProcessor.Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
                .Throws<Exception>();
            var logger = new Mock<ILog>();
            var middleware = new ErrorHandlingMiddleware(logger.Object);
            //act & assert
            middleware.Invoking(async x=> await x.ProcessAsync(messageStateHandler.Object, Mock.Of<IPipelineInformation>(), nextProcessor.Object, CancellationToken.None)).Should().NotThrowAsync();
            
        }

        [Test]
        public async Task Should_log_errors()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            nextProcessor.Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
                .Throws<Exception>();
            var logger = new Mock<ILog>();
            var middleware = new ErrorHandlingMiddleware(logger.Object);
            //act
            await middleware.ProcessAsync(messageStateHandler.Object, Mock.Of<IPipelineInformation>(), nextProcessor.Object, CancellationToken.None);
            //assert
            logger.Verify(x => x.Error(It.IsAny<Exception>(), "Error processing message {@TestCommand}", It.IsAny<TestCommand>()), Times.Once);
        }

        [Test]
        public async Task Should_abandon_message_on_errors()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            nextProcessor.Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
                .Throws<Exception>();
            var logger = new Mock<ILog>();
            var middleware = new ErrorHandlingMiddleware(logger.Object);
            //act
            await middleware.ProcessAsync(messageStateHandler.Object, Mock.Of<IPipelineInformation>(), nextProcessor.Object, CancellationToken.None);
            //assert
            messageStateHandler.Verify(x=> x.AbandonByErrorAsync(It.IsAny<Exception>()), Times.Once);
        }
        [Test]
        public void Should_not_throw_when_abandon_message_on_errors_fails()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            messageStateHandler.Setup(x => x.AbandonByErrorAsync(It.IsAny<Exception>())).Throws<Exception>();
            nextProcessor.Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
                .Throws<Exception>();
            var logger = new Mock<ILog>();
            var middleware = new ErrorHandlingMiddleware(logger.Object);
            //act
            middleware.Invoking(async x => await x.ProcessAsync(messageStateHandler.Object, Mock.Of<IPipelineInformation>(), nextProcessor.Object, CancellationToken.None)).Should().NotThrowAsync();
            //assert
            logger.Verify(x => x.Error(It.IsAny<Exception>(), "Failed to abandon message {@TestCommand}", It.IsAny<TestCommand>()), Times.Once);
        }
    }
}