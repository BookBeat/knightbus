using System;
using FluentAssertions;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit
{
    [TestFixture]
    public class MessageMapperTests
    {
        [Test]
        public void Should_find_registered_mapping()
        {
            // align
            const string name = "queue";
            MessageMapper.RegisterMapping(typeof(RegisteredMessageMapping), name);

            // act
            var queueName = MessageMapper.GetQueueName(typeof(RegisteredMessageMapping));

            // assert
            queueName.Should().Be(name);
        }

        [Test]
        public void Should_throw_for_non_registered_message()
        {
            var action = new Func<string>(() => MessageMapper.GetQueueName(typeof(NotRegisteredMessage)));
            action.Invoking(m => m.Invoke()).Should().Throw<MessageMappingMissingException>();
        }

        [Test]
        public void Should_map_assembly()
        {
            // align

            // act
            MessageMapper.RegisterMappingsFromAssembly(typeof(MessageMapperRegisteredCommand).Assembly);
            var name = MessageMapper.GetQueueName(typeof(MessageMapperRegisteredCommand));

            // assert
            name.Should().Be("awesome-queue");
        }
        
    }

    public class MessageMapperRegisteredCommand : ICommand
    {
        public string MessageId { get; set; }
    }
    public class MessageMapperRegisteredCommandMapping : IMessageMapping<MessageMapperRegisteredCommand>
    {
        public string QueueName => "awesome-queue";
    }
}
