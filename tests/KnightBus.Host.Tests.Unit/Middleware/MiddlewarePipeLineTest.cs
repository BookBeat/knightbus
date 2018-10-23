using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Messages;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit.Middleware
{
    [TestFixture]
    public class MiddlewarePipelineTest
    {
        [Test]
        public async Task Should_execute_ordered_pipeline()
        {
            //arrange
            var executionOrderList = new List<int>();
            var middlewares = new List<IMessageProcessorMiddleware>();
            for (var i = 9; i >= 0; i--)
            {
                middlewares.Add(new TestMiddleware(executionOrderList, i));
            }
            var transportConfig = new Mock<ITransportConfiguration>();
            transportConfig.Setup(x => x.Middlewares).Returns(middlewares);
            var finalProcessor = new Mock<IMessageProcessor>();
            var pipeline = new MiddlewarePipeline(new List<IMessageProcessorMiddleware>(), transportConfig.Object, Mock.Of<ILog>());
            //act
            var chain = pipeline.GetPipeline(finalProcessor.Object);
            await chain.ProcessAsync(Mock.Of<IMessageStateHandler<TestCommand>>(), CancellationToken.None);
            //assert

            for (int i = 0; i < 10; i++)
            {
                executionOrderList[i].Should().Be(i);
            }
            finalProcessor.Verify(x=> x.ProcessAsync(It.IsAny<IMessageStateHandler<TestCommand>>(), CancellationToken.None), Times.Once);
        }

        private class TestMiddleware : IMessageProcessorMiddleware
        {
            private readonly List<int> _list;
            private readonly int _order;

            public TestMiddleware(List<int> list, int order)
            {
                _list = list;
                _order = order;
            }
            public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
            {
                await next.ProcessAsync(messageStateHandler, cancellationToken).ContinueWith(task => _list.Add(_order), cancellationToken);
            }
        }
    }
}