﻿using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.DefaultMiddlewares;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class DeadLetterMiddlewareTests
{
    [Test]
    public async Task Should_dead_letter_messages()
    {
        //arrange
        var pipeline = new Mock<IPipelineInformation>();
        var di = new Mock<IDependencyInjection>();
        var hostConfiguration = new Mock<IHostConfiguration>();
        hostConfiguration.Setup(x => x.DependencyInjection).Returns(di.Object);
        pipeline.Setup(x => x.HostConfiguration).Returns(hostConfiguration.Object);
        var nextProcessor = new Mock<IMessageProcessor>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        messageStateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
        messageStateHandler.Setup(x => x.DeliveryCount).Returns(2);
        messageStateHandler.Setup(x => x.MessageScope).Returns(di.Object);
        var middleware = new DeadLetterMiddleware();
        //act
        await middleware.ProcessAsync(
            messageStateHandler.Object,
            pipeline.Object,
            nextProcessor.Object,
            CancellationToken.None
        );
        //assert
        messageStateHandler.Verify(x => x.DeadLetterAsync(1), Times.Once);
    }

    [Test]
    public async Task Should_not_continue_after_dead_letter_messages()
    {
        //arrange
        var pipeline = new Mock<IPipelineInformation>();
        var di = new Mock<IDependencyInjection>();
        var hostConfiguration = new Mock<IHostConfiguration>();
        hostConfiguration.Setup(x => x.DependencyInjection).Returns(di.Object);
        pipeline.Setup(x => x.HostConfiguration).Returns(hostConfiguration.Object);
        var nextProcessor = new Mock<IMessageProcessor>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        messageStateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
        messageStateHandler.Setup(x => x.DeliveryCount).Returns(2);
        messageStateHandler.Setup(x => x.MessageScope).Returns(di.Object);
        var middleware = new DeadLetterMiddleware();
        //act
        await middleware.ProcessAsync(
            messageStateHandler.Object,
            pipeline.Object,
            nextProcessor.Object,
            CancellationToken.None
        );
        //assert
        nextProcessor.Verify(
            x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None),
            Times.Never
        );
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
        await middleware.ProcessAsync(
            messageStateHandler.Object,
            Mock.Of<IPipelineInformation>(),
            nextProcessor.Object,
            CancellationToken.None
        );
        //assert
        messageStateHandler.Verify(x => x.DeadLetterAsync(1), Times.Never);
        nextProcessor.Verify(
            x => x.ProcessAsync(messageStateHandler.Object, CancellationToken.None),
            Times.Once
        );
    }

    [Test]
    public async Task Should_call_BeforeDeadLetterAsync_before_dead_letter_messages()
    {
        //arrange
        var countable = new Mock<ICountable>();
        var processor = new DeadLetterTestProcessor(countable.Object);

        var di = new Mock<IDependencyInjection>();
        di.Setup(x => x.GetInstance<object>(It.IsAny<Type>())).Returns(processor);

        var hostConfiguration = new Mock<IHostConfiguration>();
        hostConfiguration.Setup(x => x.Log).Returns(Mock.Of<ILogger>());
        hostConfiguration.Setup(x => x.DependencyInjection).Returns(di.Object);

        var pipeline = new Mock<IPipelineInformation>();
        pipeline.Setup(x => x.HostConfiguration).Returns(hostConfiguration.Object);

        var nextProcessor = new Mock<IMessageProcessor>();
        var messageStateHandler = new Mock<IMessageStateHandler<TestCommand>>();
        messageStateHandler.Setup(x => x.DeadLetterDeliveryLimit).Returns(1);
        messageStateHandler.Setup(x => x.DeliveryCount).Returns(2);
        messageStateHandler.Setup(x => x.GetMessage()).Returns(new TestCommand());
        messageStateHandler.Setup(x => x.MessageScope).Returns(di.Object);
        var middleware = new DeadLetterMiddleware();
        //act
        await middleware.ProcessAsync(
            messageStateHandler.Object,
            pipeline.Object,
            nextProcessor.Object,
            CancellationToken.None
        );
        //assert
        countable.Verify(x => x.Count(), Times.Once);
        messageStateHandler.Verify(x => x.DeadLetterAsync(1), Times.Once);
    }

    private class DeadLetterTestProcessor
        : IProcessBeforeDeadLetter<TestCommand>,
            IProcessCommand<TestCommand, DeadLetterTestProcessor.TestSettings>
    {
        private readonly ICountable _countable;

        public DeadLetterTestProcessor(ICountable countable)
        {
            _countable = countable;
        }

        public Task BeforeDeadLetterAsync(TestCommand message, CancellationToken cancellationToken)
        {
            _countable.Count();
            return Task.CompletedTask;
        }

        public Task ProcessAsync(TestCommand message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal class TestSettings : IProcessingSettings
        {
            public int MaxConcurrentCalls => 1;
            public int PrefetchCount => 1;
            public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(1);
            public int DeadLetterDeliveryLimit => 1;
        }
    }
}
