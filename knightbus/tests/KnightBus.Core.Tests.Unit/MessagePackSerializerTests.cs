using System;
using FluentAssertions;
using KnightBus.MessagePack;
using KnightBus.Messages;
using MessagePack;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
    [TestFixture]
    public class MessagePackSerializerTests
    {
        [Test]
        public void Should_serialize_deserialize_message()
        {
            //arrange
            var serializer = new MessagePackCSharpSerializer();
            var original = new TestSerializationCommand
            {
                Age = 1,
                Height = 5.7f,
                Name = "Aurora"
            };
            var serialized = serializer.Serialize(original);
            //act
            var deserialized = serializer.Deserialize<TestSerializationCommand>(serialized.AsSpan());
            //assert
            deserialized.Age.Should().Be(original.Age);
            deserialized.Name.Should().Be(original.Name);
            deserialized.Height.Should().Be(original.Height);
        }

        [MessagePackObject]
        public class TestSerializationCommand : ICommand
        {
            [Key(1)]
            public string Name { get; set; }
            [Key(2)]
            public int Age { get; set; }
            [Key(3)]
            public float Height { get; set; }
        }
    }
}
