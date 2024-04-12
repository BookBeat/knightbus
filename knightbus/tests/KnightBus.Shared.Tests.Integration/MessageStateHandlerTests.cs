using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Messages;
using NUnit.Framework;

namespace KnightBus.Shared.Tests.Integration;

[TestFixture]
public abstract class MessageStateHandlerTests<TCommand> where TCommand : class, IMessage
{
    [SetUp]
    public abstract Task Setup();
    protected abstract Task<List<TCommand>> GetMessages(int count);
    protected abstract Task<List<TCommand>> GetDeadLetterMessages(int count);
    protected abstract Task SendMessage(string message);
    protected abstract Task<IMessageStateHandler<TCommand>> GetMessageStateHandler();

    [Test]
    public async Task Should_complete_the_message_and_remove_it_from_the_queue()
    {
        //arrange
        await SendMessage("Testing Complete");
        var stateHandler = await GetMessageStateHandler();

        //act
        await stateHandler.CompleteAsync();

        //assert
        var messages = await GetMessages(1);
        messages.Should().BeEmpty();
    }

    [Test]
    public async Task Should_abandon_the_message_and_return_it_to_the_queue()
    {
        //arrange
        await SendMessage("Testing Abandon");
        var stateHandler = await GetMessageStateHandler();

        //act
        await stateHandler.AbandonByErrorAsync(new Exception());

        //assert
        var messages = await GetMessages(10);
        messages.Should().HaveCount(1);
    }
    [Test]
    public async Task Should_dead_letter_the_message_and_move_it_to_the_dl_queue()
    {
        //arrange
        await SendMessage("Testing Dead Letters");
        var stateHandler = await GetMessageStateHandler();

        //act
        await stateHandler.DeadLetterAsync(0);

        //assert
        var messages = await GetMessages(1);
        messages.Should().HaveCount(0);
        var deadLetters = await GetDeadLetterMessages(10);
        deadLetters.Count.Should().Be(1);
    }
}
