using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.Management;
using KnightBus.Messages;
using NUnit.Framework;

namespace KnightBus.Shared.Tests.Integration;

[TestFixture]
public abstract class QueueManagerTests<TCommand> where TCommand : class, IMessage
{
    protected IQueueManager QueueManager { get; set; }
    protected QueueType QueueType { get; set; }

    [SetUp]
    public abstract Task Setup();

    public abstract Task<string> CreateQueue();
    public abstract Task<string> SendMessage(string message);

    public abstract Task<IMessageStateHandler<TCommand>> GetMessageStateHandler(string queueName);

    [Test]
    public void Should_specify_queue_type()
    {
        //act && assert
        QueueManager.QueueType.Should().Be(QueueType);
    }

    [Test]
    public async Task Should_list_queues()
    {
        //arrange
        var queueNames = new List<string>();
        var count = 11;
        for (var i = 0; i < count; i++)
        {
            queueNames.Add(await CreateQueue());
        }

        //act
        var queues = (await QueueManager.List(CancellationToken.None)).Select(q => q.Name).ToArray();

        //assert
        queues.Should().HaveCount(count);
        queueNames.Should().BeEquivalentTo(queues, options => options.WithoutStrictOrdering());
    }

    [Test]
    public async Task Should_peek_message_without_deleting_it()
    {
        //arrange
        var messageText = Guid.NewGuid().ToString("N");
        var queueName = await SendMessage(messageText);
        //act
        var messages = await QueueManager.Peek(queueName, 10, CancellationToken.None);
        //assert
        messages.Should().HaveCount(1);
        messages[0].Body.Should().Contain(messageText);
        var verifyStillAvailable = await QueueManager.Peek(queueName, 1, CancellationToken.None);
        verifyStillAvailable[0].Body.Should().Contain(messageText, "The message should still be available after peeking");
    }
    [Test]
    public async Task Should_peek_dead_letter_message_without_deleting_it()
    {
        //arrange
        var messageText = Guid.NewGuid().ToString("N");
        var queueName = await SendMessage(messageText);
        var stateHandler = await GetMessageStateHandler(queueName);
        await stateHandler.DeadLetterAsync(1);
        //act
        var messages = await QueueManager.PeekDeadLetter(queueName, 10, CancellationToken.None);
        //assert
        messages.Should().HaveCount(1);
        messages[0].Body.Should().Contain(messageText);
        var verifyStillAvailable = await QueueManager.PeekDeadLetter(queueName, 1, CancellationToken.None);
        verifyStillAvailable[0].Body.Should().Contain(messageText, "The message should still be available after peeking");
    }

    [Test]
    public async Task Should_receive_dead_letter_message_and_delete_it()
    {
        //arrange
        var messageText = Guid.NewGuid().ToString("N");
        var queueName = await SendMessage(messageText);
        var stateHandler = await GetMessageStateHandler(queueName);
        await stateHandler.DeadLetterAsync(1);
        //act
        var messages = await QueueManager.ReadDeadLetter(queueName, 10, CancellationToken.None);
        //assert
        messages.Should().HaveCount(1);
        messages[0].Body.Should().Contain(messageText);
        var verifyStillAvailable = await QueueManager.PeekDeadLetter(queueName, 1, CancellationToken.None);
        verifyStillAvailable.Should().BeEmpty("The message should be deleted after reading");
    }

    [Test]
    public async Task Should_move_dead_letter_message_to_original_queue()
    {
        //arrange
        var messageText = Guid.NewGuid().ToString("N");
        var queueName = await SendMessage(messageText);
        var stateHandler = await GetMessageStateHandler(queueName);
        await stateHandler.DeadLetterAsync(1);
        //act
        var noMoved = await QueueManager.MoveDeadLetters(queueName, 1, CancellationToken.None);
        //assert
        noMoved.Should().Be(1);
        var deadLetterMessages = await QueueManager.PeekDeadLetter(queueName, 10, CancellationToken.None);
        deadLetterMessages.Count.Should().Be(0);
        var messages = await QueueManager.Peek(queueName, 10, CancellationToken.None);
        messages.Count.Should().Be(1);
        messages[0].Body.Should().Contain(messageText);
    }

    [Test]
    public async Task Should_reflect_current_queue_properties()
    {
        //arrange
        var queueName = await SendMessage(Guid.NewGuid().ToString("N"));
        await SendMessage(Guid.NewGuid().ToString("N"));
        var stateHandler = await GetMessageStateHandler(queueName);
        await stateHandler.DeadLetterAsync(1);
        //act
        var state = await QueueManager.Get(queueName, CancellationToken.None);
        //assert
        state.Name.Should().Be(queueName);
        state.ActiveMessageCount.Should().Be(1);
        state.DeadLetterMessageCount.Should().Be(1);
    }
}
