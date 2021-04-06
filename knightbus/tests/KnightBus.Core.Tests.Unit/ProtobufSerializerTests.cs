using FluentAssertions;
using KnightBus.Messages;
using KnightBus.ProtobufNet;
using NUnit.Framework;
using ProtoBuf;

namespace KnightBus.Core.Tests.Unit
{
    [TestFixture]
    public class ProtobufSerializerTests
    {
        [Test]
        public void Should_serialize_deserialize_message()
        {
            //arrange
            var serializer = new ProtobufNetSerializer();
            var original = new TestSerializationCommand
            {
                Age = 1,
                Height = 5.7f,
                Name = "Aurora"
            };
            var serialized = serializer.Serialize(original);
            //act
            var deserialized = serializer.Deserialize<TestSerializationCommand>(serialized);
            //assert
            deserialized.Age.Should().Be(original.Age);
            deserialized.Name.Should().Be(original.Name);
            deserialized.Height.Should().Be(original.Height);
        }

        [ProtoContract]
        public class TestSerializationCommand : ICommand
        {
            [ProtoMember(1)]
            public string Name { get; set; }
            [ProtoMember(2)]
            public int Age { get; set; }
            [ProtoMember(3)]
            public float Height { get; set; }
        }
    }
}