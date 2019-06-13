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

        [Test]
        public void Should_throw_unmapped_message()
        {
            //arrange
            var mapper = new SagaMessageMapper();
            //act & assert
            mapper.Invoking(x=> x.GetMapping<UnMappedMessage>()).Should().Throw<SagaMessageMappingNotFoundException>();
        }
        
        internal class TestMessage :IMessage
        {
            public string Id { get; set; }
        }
        internal class UnMappedMessage : IMessage
        {
        }
    }
}