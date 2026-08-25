using System;
using System.IO;
using FluentAssertions;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class JsonSerializerTests
{
    [Test]
    public void Should_not_serialize_attachments()
    {
        //arrange
        var serializer = new MicrosoftJsonSerializer();
        var message = new AttachmentCommand
        {
            Message = "Hello",
            Attachment = new MessageAttachment("filename.txt", "text/plain", new MemoryStream()),
        };
        //act
        var serialized = serializer.Serialize(message);
        var deserialized = serializer.Deserialize<AttachmentCommand>(serialized.AsSpan());
        //assert
        deserialized.Attachment.Should().BeNull();
        deserialized.Message.Should().Be("Hello");
    }

    [Test]
    public void Should_serialize_deserialize_message()
    {
        //arrange
        var serializer = new MicrosoftJsonSerializer();
        var original = new TestSerializationCommand
        {
            Age = 1,
            Height = 5.7f,
            Name = "Aurora",
        };
        var serialized = serializer.Serialize(original);
        //act
        var deserialized = serializer.Deserialize<TestSerializationCommand>(serialized.AsSpan());
        //assert
        deserialized.Age.Should().Be(original.Age);
        deserialized.Name.Should().Be(original.Name);
        deserialized.Height.Should().Be(original.Height);
    }

    [Test]
    public void Newtonsoft_Should_not_serialize_attachments()
    {
        //arrange
        var serializer = new NewtonsoftSerializer();
        var message = new AttachmentCommand
        {
            Message = "Hello",
            Attachment = new MessageAttachment("filename.txt", "text/plain", new MemoryStream()),
        };
        //act
        var serialized = serializer.Serialize(message);
        var deserialized = serializer.Deserialize<AttachmentCommand>(serialized.AsSpan());
        //assert
        deserialized.Attachment.Should().BeNull();
        deserialized.Message.Should().Be("Hello");
    }

    [Test]
    public void Newtonsoft_Should_serialize_deserialize_message()
    {
        //arrange
        var serializer = new NewtonsoftSerializer();
        var original = new TestSerializationCommand
        {
            Age = 1,
            Height = 5.7f,
            Name = "Aurora",
        };
        var serialized = serializer.Serialize(original);
        //act
        var deserialized = serializer.Deserialize<TestSerializationCommand>(serialized.AsSpan());
        //assert
        deserialized.Age.Should().Be(original.Age);
        deserialized.Name.Should().Be(original.Name);
        deserialized.Height.Should().Be(original.Height);
    }

    public class TestSerializationCommand : ICommand
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public float Height { get; set; }
    }
}
