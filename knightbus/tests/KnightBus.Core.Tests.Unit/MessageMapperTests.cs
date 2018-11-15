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
        public void Should_throw_for_non_registered_message()
        {
            var action = new Func<string>(AutoMessageMapper.GetQueueName<NotRegisteredMessage>);
            action.Invoking(m => m.Invoke()).Should().Throw<MessageMappingMissingException>();
        }
        [Test]
        public void Should_find_registered_generic()
        {
            var name = AutoMessageMapper.GetQueueName<RegisteredCommand>();
            name.Should().Be("queue");
        }
        [Test]
        public void Should_find_registered_type()
        {
            var name = AutoMessageMapper.GetQueueName(typeof(RegisteredCommand));
            name.Should().Be("queue");
        }
    }

    public class NotRegisteredMessage : ICommand
    {
        public string MessageId { get; set; }
    }
    public class RegisteredCommand : ICommand
    {
        public string MessageId { get; set; }
    }
    public class RegisteredMessageMapping : IMessageMapping<RegisteredCommand>
    {
        public string QueueName => "queue";
    }
}
