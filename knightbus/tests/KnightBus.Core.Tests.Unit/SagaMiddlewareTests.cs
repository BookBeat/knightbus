using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using KnightBus.Messages;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
    [TestFixture]
    public class SagaMiddlewareTests
    {
        [Test]
        public async Task Should_complete_message_if_saga_already_is_started()
        {
            //arrange
            var partitionKey = "a";
            var id = "b";
            var sagaStore = new Mock<ISagaStore>();
            sagaStore.Setup(x => x.Create<SagaData>(partitionKey, id, It.IsAny<SagaData>(), TimeSpan.FromMinutes(1))).ThrowsAsync(new SagaAlreadyStartedException(partitionKey, id));

            var di = new Mock<IDependencyInjection>();
            di.Setup(x => x.GetInstance<IProcessMessage<SagaStartMessage>>(typeof(IProcessCommand<SagaStartMessage, Settings>))).Returns(new Saga());

            var hostConfiguration = new Mock<IHostConfiguration>();
            hostConfiguration.Setup(x => x.Log).Returns(Mock.Of<ILog>());
            hostConfiguration.Setup(x => x.DependencyInjection).Returns(di.Object); // TODO: setup dependency injection
            var messageStateHandler = new Mock<IMessageStateHandler<SagaStartMessage>>();
            messageStateHandler.Setup(x => x.MessageScope).Returns(di.Object);
            var pipelineInformation = new Mock<IPipelineInformation>();
            pipelineInformation.Setup(x => x.HostConfiguration).Returns(hostConfiguration.Object);
            pipelineInformation.Setup(x => x.ProcessorInterfaceType).Returns(typeof(IProcessCommand<SagaStartMessage, Settings>));
            

            var middleware = new SagaMiddleware(sagaStore.Object);

            //act
            await middleware.ProcessAsync(messageStateHandler.Object, pipelineInformation.Object, null, CancellationToken.None);
            //assert
            messageStateHandler.Verify(x => x.CompleteAsync(), Times.Once);
        }

        [Test]
        public async Task Should_load_sagadata_and_call_next_when_success()
        {
            //arrange
            var partitionKey = "a";
            var id = "b";
            var sagaStore = new Mock<ISagaStore>();
            sagaStore.Setup(x => x.Create(partitionKey, id, It.IsAny<SagaData>(), TimeSpan.FromMinutes(1))).ReturnsAsync(new SagaData{Data = "loaded"});

            var saga = new Saga();

            var di = new Mock<IDependencyInjection>();
            di.Setup(x => x.GetInstance<IProcessMessage<SagaStartMessage>>(typeof(IProcessCommand<SagaStartMessage, Settings>))).Returns(saga);

            var hostConfiguration = new Mock<IHostConfiguration>();
            hostConfiguration.Setup(x => x.Log).Returns(Mock.Of<ILog>());
            hostConfiguration.Setup(x => x.DependencyInjection).Returns(di.Object);

            var messageStateHandler = new Mock<IMessageStateHandler<SagaStartMessage>>();
            messageStateHandler.Setup(x => x.MessageScope).Returns(di.Object);
            var pipelineInformation = new Mock<IPipelineInformation>();
            pipelineInformation.Setup(x => x.HostConfiguration).Returns(hostConfiguration.Object);
            pipelineInformation.Setup(x => x.ProcessorInterfaceType).Returns(typeof(IProcessCommand<SagaStartMessage, Settings>));

            var next = new Mock<IMessageProcessor>();

            var middleware = new SagaMiddleware(sagaStore.Object);

            //act
            await middleware.ProcessAsync(messageStateHandler.Object, pipelineInformation.Object, next.Object, CancellationToken.None);
            //assert
            next.Verify(x=> x.ProcessAsync(messageStateHandler.Object, CancellationToken.None), Times.Once);
            saga.Data.Data.Should().Be("loaded");
        }

        public class Saga : Saga<SagaData>, IProcessCommand<SagaStartMessage, Settings>
        {
            public override string PartitionKey => "a";

            public Saga()
            {
                MessageMapper.MapStartMessage<SagaStartMessage>(m => "b");
            }

            public Task ProcessAsync(SagaStartMessage message, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        public class SagaData
        {
            public string Data { get; set; }
        }

        public class SagaStartMessage : ICommand { }

        public class Settings:IProcessingSettings
        {
            public int MaxConcurrentCalls { get; }
            public int PrefetchCount { get; }
            public TimeSpan MessageLockTimeout { get; }
            public int DeadLetterDeliveryLimit { get; }
        }
    }
}