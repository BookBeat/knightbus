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
            for (var i = 0; i < 10; i++)
            {
                middlewares.Add(new TestMiddleware(executionOrderList, i));
            }
            var transportConfig = new Mock<ITransportChannelFactory>();
            transportConfig.Setup(x => x.Middlewares).Returns(middlewares);
            var finalProcessor = new Mock<IMessageProcessor>();
            var pipeline = new MiddlewarePipeline(new List<IMessageProcessorMiddleware>(), Mock.Of<IPipelineInformation>(), transportConfig.Object, Mock.Of<ILog>());
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

        [Test]
        public async Task Should_move_message_scope_provider_right_after_error_middleware()
        {
            //arrange
            var executionOrderList = new List<int>();
            var middlewares = new List<IMessageProcessorMiddleware>();
            for (var i = 1; i < 11; i++)
            {
                middlewares.Add(new TestMiddleware(executionOrderList, i));
            }

            middlewares.Add(new MessageScopeTestMiddleware(executionOrderList, 0));


            var transportConfig = new Mock<ITransportChannelFactory>();
            transportConfig.Setup(x => x.Middlewares).Returns(new List<IMessageProcessorMiddleware>());
            var finalProcessor = new Mock<IMessageProcessor>();
            var pipeline = new MiddlewarePipeline(middlewares, Mock.Of<IPipelineInformation>(), transportConfig.Object, Mock.Of<ILog>());
            //act
            var chain = pipeline.GetPipeline(finalProcessor.Object);
            await chain.ProcessAsync(Mock.Of<IMessageStateHandler<TestCommand>>(), CancellationToken.None);
            //assert

            for (int i = 0; i < 11; i++)
            {
                executionOrderList[i].Should().Be(i);
            }

            executionOrderList.Count.Should().Be(11);
            finalProcessor.Verify(x => x.ProcessAsync(It.IsAny<IMessageStateHandler<TestCommand>>(), CancellationToken.None), Times.Once);
        }

        private class TestMiddleware : IMessageProcessorMiddleware
        {
            public readonly List<int> _list;
            public readonly int _order;

            public TestMiddleware(List<int> list, int order)
            {
                _list = list;
                _order = order;
            }
            public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
            {
                _list.Add(_order);
                await next.ProcessAsync(messageStateHandler, cancellationToken);
            }
        }

        private class MessageScopeTestMiddleware : TestMiddleware, IMessageScopeProviderMiddleware
        {
            public MessageScopeTestMiddleware(List<int> list, int order) : base(list, order)
            {
            }
        }
    }
}