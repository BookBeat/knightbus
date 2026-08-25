using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core.Sagas;
using KnightBus.Messages;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class SagaHandlerTests
{
    [Test]
    public async Task Initialize_should_start_new_saga_when_start_message_is_received()
    {
        //arrange
        var id = "something-unique";
        var store = new Mock<ISagaStore>();
        var saga = new TestSaga();
        var startMessage = new TestSagaStartMessage(id);
        var handler = new SagaHandler<TestSagaData, TestSagaStartMessage>(
            store.Object,
            saga,
            startMessage
        );
        //act
        await handler.Initialize(CancellationToken.None);
        //assert
        store.Verify(
            x =>
                x.Create(
                    saga.PartitionKey,
                    id,
                    It.IsAny<TestSagaData>(),
                    TimeSpan.FromHours(1),
                    CancellationToken.None
                ),
            Times.Once
        );
    }

    [Test]
    public async Task Initialize_should_resume_saga_when_start_message_is_received()
    {
        //arrange
        var id = "something-unique";
        var store = new Mock<ISagaStore>();
        var saga = new TestSaga();
        var startMessage = new TestSagaMessage(id);
        var handler = new SagaHandler<TestSagaData, TestSagaMessage>(
            store.Object,
            saga,
            startMessage
        );
        saga.MessageMapper.MapMessage<TestSagaMessage>(x => x.MessageId);
        //act
        await handler.Initialize(CancellationToken.None);
        //assert
        store.Verify(
            x => x.GetSaga<TestSagaData>(saga.PartitionKey, id, CancellationToken.None),
            Times.Once
        );
    }

    [Test]
    public async Task Initialize_should_throw_when_message_is_not_mapped()
    {
        //arrange
        var id = "something-unique";
        var store = new Mock<ISagaStore>();
        var saga = new TestSaga();
        var startMessage = new TestSagaMessage(id);
        var handler = new SagaHandler<TestSagaData, TestSagaMessage>(
            store.Object,
            saga,
            startMessage
        );
        //act & assert
        await handler
            .Awaiting(x => x.Initialize(CancellationToken.None))
            .Should()
            .ThrowAsync<SagaMessageMappingNotFoundException>();
    }

    internal class TestSagaData { }

    internal class TestSaga : Saga<TestSagaData>
    {
        public override TimeSpan TimeToLive => TimeSpan.FromHours(1);

        public TestSaga()
        {
            MessageMapper.MapStartMessage<TestSagaStartMessage>(m => m.MessageId);
        }

        public override string PartitionKey => "saga-id";
    }

    internal class TestSagaStartMessage : IMessage
    {
        public TestSagaStartMessage(string id)
        {
            MessageId = id;
        }

        public string MessageId { get; }
    }

    internal class TestSagaMessage : IMessage
    {
        public TestSagaMessage(string id)
        {
            MessageId = id;
        }

        public string MessageId { get; }
    }
}
