using FluentAssertions;
using KnightBus.Core.Sagas;
using KnightBus.Messages;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
    [TestFixture]
    public class SagaMapperTests
    {
        [Test]
        public void Should_map_start_message()
        {
            //arrange
            var mapper = new SagaMessageMapper();
            mapper.MapStartMessage<TestMessage>(m=> m.Id);
            //act
            var mapping = mapper.GetMapping<TestMessage>();
            //assert
            mapping.Should().NotBeNull();
            mapper.IsStartMessage(typeof(TestMessage)).Should().BeTrue();
            mapping.Invoke(new TestMessage {Id = "an_id"}).Should().Be("an_id");
        }

        [Test]
        public void Should_map_message()
        {
            //arrange
            var mapper = new SagaMessageMapper();
            mapper.MapMessage<TestMessage>(m => m.Id);
            //act
            var mapping = mapper.GetMapping<TestMessage>();
            //assert
            mapping.Should().NotBeNull();
            mapper.IsStartMessage(typeof(TestMessage)).Should().BeFalse();
            mapping.Invoke(new TestMessage { Id = "an_id" }).Should().Be("an_id");
        }

        internal class TestSaga : Saga<TestSagaData>
        {
            public TestSaga()
            {
                MessageMapper.MapStartMessage<TestMessage>(m=> m.Id);
            }
        }
        internal class TestSagaData:ISagaData
        {
            public string Key { get; }
        }
        internal class TestMessage :IMessage
        {
            public string Id { get; set; }
        }
    }
}