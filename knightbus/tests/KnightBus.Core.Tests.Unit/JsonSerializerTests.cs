using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
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
                Attachment = new MessageAttachment("filename.txt", "text/plain", new MemoryStream())
            };
            //act
            var serialized = serializer.Serialize(message);
            var deserialized = serializer.Deserialize<AttachmentCommand>(serialized);
            //assert
            deserialized.Attachment.Should().BeNull();
            deserialized.Message.Should().Be("Hello");
        }
    }
}