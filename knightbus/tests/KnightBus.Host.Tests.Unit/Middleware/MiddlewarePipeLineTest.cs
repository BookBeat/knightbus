using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host.MessageProcessing.Processors;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Host.Tests.Unit.Middleware
{
    [TestFixture]
    public class MiddlewarePipelineTest
    {
        
        [Test]
        public async Task Should_execute_ordered_pipeline_from_microsoft_di()
        {
            //arrange
            var executionOrderList = new List<int>();
            var services = new ServiceCollection();
            for (var i = 0; i < 10; i++)
            {
                services.AddMiddleware(new TestMiddleware(executionOrderList, i));
            }
            var finalProcessor = new Mock<IMessageProcessor>();
            var provider = services.BuildServiceProvider();
            var pipeline = new MiddlewarePipeline(provider.GetServices<IMessageProcessorMiddleware>(), Mock.Of<IPipelineInformation>(), Mock.Of<ILogger>());
            //act
            var chain = pipeline.GetPipeline(finalProcessor.Object);
            await chain.ProcessAsync(Mock.Of<IMessageStateHandler<TestCommand>>(), CancellationToken.None);
            //assert

            for (int i = 0; i < 10; i++)
            {
                executionOrderList[i].Should().Be(i);
            }
            finalProcessor.Verify(x => x.ProcessAsync(It.IsAny<IMessageStateHandler<TestCommand>>(), CancellationToken.None), Times.Once);
        }
        
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
            var finalProcessor = new Mock<IMessageProcessor>();
            var pipeline = new MiddlewarePipeline(middlewares, Mock.Of<IPipelineInformation>(), Mock.Of<ILogger>());
            //act
            var chain = pipeline.GetPipeline(finalProcessor.Object);
            await chain.ProcessAsync(Mock.Of<IMessageStateHandler<TestCommand>>(), CancellationToken.None);
            //assert

            for (int i = 0; i < 10; i++)
            {
                executionOrderList[i].Should().Be(i);
            }
            finalProcessor.Verify(x => x.ProcessAsync(It.IsAny<IMessageStateHandler<TestCommand>>(), CancellationToken.None), Times.Once);
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


            var finalProcessor = new Mock<IMessageProcessor>();
            var pipeline = new MiddlewarePipeline(middlewares, Mock.Of<IPipelineInformation>(), Mock.Of<ILogger>());
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

        [Test]
        public async Task Should_execute_scoped_MessageProcessor_with_dependency_injection()
        {
            //arrange
            var container = new ServiceCollection();
            var countableMock = new Mock<ICountable>();
            container.AddSingleton(countableMock.Object);

            container.RegisterProcessors(Assembly.GetExecutingAssembly());
            var hostConfiguration = new HostConfiguration{DependencyInjection = new MicrosoftDependencyInjection(container.BuildServiceProvider(new ServiceProviderOptions {ValidateScopes = true, ValidateOnBuild = true}))};
            
            var pipelineInformation = new Mock<IPipelineInformation>();
            pipelineInformation.Setup(x => x.HostConfiguration).Returns(hostConfiguration);

            var finalProcessor = new MessageProcessor(typeof(IProcessCommand<DiTestMessage, DiTestMessageSettings>));
            var pipeline = new MiddlewarePipeline(new List<IMessageProcessorMiddleware>(), pipelineInformation.Object, Mock.Of<ILogger>());

            var messageStateHandler = new Mock<IMessageStateHandler<DiTestMessage>>();
            messageStateHandler.Setup(x => x.MessageScope).Returns(hostConfiguration.DependencyInjection.GetScope);

            //act
            var chain = pipeline.GetPipeline(finalProcessor);
            await chain.ProcessAsync(messageStateHandler.Object, CancellationToken.None);

            //assert 
            countableMock.Verify(x => x.Count(), Times.Once);
        }

        private class MessageScopeTestMiddleware : TestMiddleware, IMessageScopeProviderMiddleware
        {
            public MessageScopeTestMiddleware(List<int> list, int order) : base(list, order)
            {
            }
        }

        public class DiTestMessage : ICommand
        {

        }

        public class DiTestMessageSettings : IProcessingSettings
        {
            public int MaxConcurrentCalls { get; set; } = 1;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
            public int DeadLetterDeliveryLimit { get; set; } = 1;
            public int PrefetchCount { get; set; }
        }


        public class DiTestCommandHandler : IProcessCommand<DiTestMessage, DiTestMessageSettings>
        {
            private readonly ICountable _countable;

            public DiTestCommandHandler(ICountable countable)
            {
                _countable = countable;
            }
            public Task ProcessAsync(DiTestMessage message, CancellationToken cancellationToken)
            {
                _countable.Count();
                return Task.CompletedTask;
            }
        }

        private class TestMessageStateHandler : IMessageStateHandler<TestCommand>
        {
            public int DeliveryCount { get; }
            public int DeadLetterDeliveryLimit { get; }
            public IDictionary<string, string> MessageProperties { get; }
            public Task CompleteAsync()
            {
                throw new NotImplementedException();
            }

            public Task ReplyAsync<TReply>(TReply reply)
            {
                throw new NotImplementedException();
            }

            public Task AbandonByErrorAsync(Exception e)
            {
                throw new NotImplementedException();
            }

            public Task DeadLetterAsync(int deadLetterLimit)
            {
                throw new NotImplementedException();
            }

            public TestCommand GetMessage()
            {
                throw new NotImplementedException();
            }

            public IDependencyInjection MessageScope { get; set; }
        }
    }
}