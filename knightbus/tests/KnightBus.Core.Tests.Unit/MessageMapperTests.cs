using System;
using FluentAssertions;
using KnightBus.Core.Exceptions;
using KnightBus.Messages;
using NUnit.Framework;

namespace KnightBus.Core.Tests.Unit;

[TestFixture]
public class MessageMapperTests
{
    [Test]
    public void Should_find_registered_mapping_queue_name()
    {
        // align
        IMessageMapping queue = new TestCommandMapping();
        MessageMapper.RegisterMapping(typeof(RegisteredMessageMapping), queue);

        // act
        var queueName = MessageMapper.GetQueueName(typeof(RegisteredMessageMapping));

        // assert
        queueName.Should().Be(queue.QueueName);
    }

    [Test]
    public void Should_find_registered_mapping()
    {
        // align
        IMessageMapping testCommandMapping = new TestCommandMapping();
        MessageMapper.RegisterMapping(typeof(RegisteredMessageMapping), testCommandMapping);

        // act
        var mapping = MessageMapper.GetMapping(typeof(RegisteredMessageMapping));

        // assert
        mapping.GetType().Should().Be(typeof(TestCommandMapping));
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
        var mapping = MessageMapper.GetMapping(typeof(TestCommand));

        // assert
        name.Should().Be("awesome-queue");
        mapping.GetType().Should().Be(typeof(TestCommandMapping));
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
