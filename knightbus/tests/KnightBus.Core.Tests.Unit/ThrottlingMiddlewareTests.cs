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
    public class ThrottlingMiddlewareTests
    {
        [Test]
        public async Task Should_release_throttle_on_exception()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            nextProcessor
                .Setup(x => x.ProcessAsync(messageStateHandler.Object, It.IsAny<CancellationToken>()))
                .Throws<Exception>();
            
            var middleware = new ThrottlingMiddleware(1);

            //act 
            await middleware
                .Awaiting(x => x.ProcessAsync(messageStateHandler.Object, Mock.Of<IPipelineInformation>(), nextProcessor.Object, CancellationToken.None))
                .Should()
                .ThrowAsync<Exception>();

            //assert
            middleware.CurrentCount.Should().Be(1);
        }

        [Test]
        public async Task Should_release_throttle_when_cancellation_token_cancelled()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            var cts = new CancellationTokenSource();
            cts.Cancel();
            nextProcessor.Setup(x => x.ProcessAsync(messageStateHandler.Object, cts.Token)).Throws<Exception>();

            var middleware = new ThrottlingMiddleware(1);
            //act 
            await middleware
                .Awaiting(x=> x.ProcessAsync(messageStateHandler.Object, Mock.Of<IPipelineInformation>(), nextProcessor.Object, cts.Token))
                .Should()
                .ThrowAsync<OperationCanceledException>();
            
            //assert
            middleware.CurrentCount.Should().Be(1);
        }

        [Test]
        public async  Task Should_throttle()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            nextProcessor.Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None))
                .Returns(Task.Delay(TimeSpan.FromSeconds(1)));
            var middleware = new ThrottlingMiddleware(2);
            //act 
            for (int i = 0; i < 10; i++)
            {
#pragma warning disable 4014
                middleware.ProcessAsync(messageStateHandler.Object, Mock.Of<IPipelineInformation>(), nextProcessor.Object, CancellationToken.None);
#pragma warning restore 4014
            }

            await Task.Delay(100);
            //assert
            middleware.CurrentCount.Should().Be(0);
            nextProcessor.Verify(x=> x.ProcessAsync(messageStateHandler.Object, CancellationToken.None), Times.Exactly(2));
        }
    }
}