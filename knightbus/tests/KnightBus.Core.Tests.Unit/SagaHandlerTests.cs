using System.Threading.Tasks;
using KnightBus.Core.Sagas;
using KnightBus.Messages;
using Moq;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
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
            var handler = new SagaHandler<TestSagaData, TestSagaStartMessage>(store.Object, saga, startMessage);
            //act
            await handler.Initialize();
            //assert
            store.Verify(x=> x.Create(id, It.IsAny<TestSagaData>()), Times.Once);
        }

        internal class TestSagaData : ISagaData
        {
            public string Key { get; }
        }
        internal class TestSaga : Saga<TestSagaData>
        {
            public TestSaga()
            {
                MessageMapper.MapStartMessage<TestSagaStartMessage>(m => m.MessageId);
            }
        }
        internal class TestSagaStartMessage : IMessage
        {
            public TestSagaStartMessage(string id)
            {
                MessageId = id;
            }
            public string MessageId { get;} 
        }
    }
}