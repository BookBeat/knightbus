using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.DefaultMiddlewares;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
    [TestFixture]
    public class DeadLetterMiddlewareTests
    {
        [Test]
        public async Task Should_dead_letter_messages()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            messageStateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
            messageStateHandler.Setup(x => x.DeliveryCount).Returns(2);
            var middleware = new DeadLetterMiddleware();
            //act
            await middleware.ProcessAsync(messageStateHandler.Object, Mock.Of<IPipelineInformation>(), nextProcessor.Object, CancellationToken.None);
            //assert
            messageStateHandler.Verify(x=> x.DeadLetterAsync(1), Times.Once);
        }

        [Test]
        public async Task Should_not_continue_after_dead_letter_messages()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            messageStateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
            messageStateHandler.Setup(x => x.DeliveryCount).Returns(2);
            var middleware = new DeadLetterMiddleware();
            //act
            await middleware.ProcessAsync(messageStateHandler.Object, Mock.Of<IPipelineInformation>(), nextProcessor.Object, CancellationToken.None);
            //assert
            nextProcessor.Verify(x=> x.ProcessAsync(messageStateHandler.Object, CancellationToken.None), Times.Never);
        }
        [Test]
        public async Task Should_continue_when_not_dead_lettering()
        {
            //arrange
            var nextProcessor = new Mock<IMessageProcessor>();
            var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
            messageStateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
            messageStateHandler.Setup(x => x.DeliveryCount).Returns(1);
            var middleware = new DeadLetterMiddleware();
            //act
            await middleware.ProcessAsync(messageStateHandler.Object, Mock.Of<IPipelineInformation>(), nextProcessor.Object, CancellationToken.None);
            //assert
            messageStateHandler.Verify(x => x.DeadLetterAsync(1), Times.Never);
            nextProcessor.Verify(x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None), Times.Once);
        }
    }
}