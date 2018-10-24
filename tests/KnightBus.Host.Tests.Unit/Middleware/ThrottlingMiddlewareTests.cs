using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Host.DefaultMiddlewares;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit.Middleware
{
    [TestFixture]
    public class ThrottlingMiddlewareTests
    {
        [Test]
        public void Should_release_throttle_on_exception()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            nextProcessor.Setup(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None)).Throws<Exception>();
            
            var middleware = new ThrottlingMiddleware(1);
            //act 
            var action = new Func<Task>(() => middleware.ProcessAsync(messageStateHandler.Object, nextProcessor.Object, CancellationToken.None));
            action.Should().Throw<Exception>();
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
                middleware.ProcessAsync(messageStateHandler.Object, nextProcessor.Object, CancellationToken.None);
#pragma warning restore 4014
            }

            await Task.Delay(100);
            //assert
            middleware.CurrentCount.Should().Be(0);
            nextProcessor.Verify(x=> x.ProcessAsync(messageStateHandler.Object, CancellationToken.None), Times.Exactly(2));
        }
    }
}