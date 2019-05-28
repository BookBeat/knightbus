using FluentAssertions;
using KnightBus.Core.Sagas;
using KnightBus.Messages;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
    [TestFixture]
    public class SagaTests
    {
        [Test]
        public void Should_map_messages()
        {
            //arrange
            var saga = new TestSaga();
            //act
            var mapping = saga.GetMapping<TestMessage>();
            //assert
            mapping.Should().NotBeNull();
            mapping.Invoke(new TestMessage {Id = "an_id"}).Should().Be("an_id");
        }

        internal class TestSaga : Saga<TestSagaData>
        {
            public TestSaga()
            {
                MapStartMessage<TestMessage>(m=> m.Id);
            }
        }
        internal class TestSagaData:ISagaData { }
        internal class TestMessage :IMessage
        {
            public string Id { get; set; }
        }
    }
}