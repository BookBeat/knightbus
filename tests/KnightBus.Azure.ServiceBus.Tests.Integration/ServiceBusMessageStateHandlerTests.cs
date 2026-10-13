using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

[TestFixture]
public class ServiceBusMessageStateHandlerTests : MessageStateHandlerTests<StateHandlerCommand>
{
    private static readonly string Queue = AutoMessageMapper.GetQueueName<StateHandlerCommand>();
    private ServiceBusClient _client = null!;
    private ServiceBusReceiver _receiver = null!;
    private readonly List<ServiceBusReceivedMessage> _locked = new();

    [OneTimeSetUp]
    public void OneTimeSetUp() => _client = new ServiceBusClient(ServiceBusSetup.ConnectionString);

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _client.DisposeAsync();

    public override async Task Setup()
    {
        await EmulatorEntities.PurgeQueue(_client, Queue);
        _receiver = _client.CreateReceiver(Queue);
    }

    [TearDown]
    public async Task TearDown()
    {
        // Release what the tests left locked so the next purge can remove it
        foreach (var message in _locked)
        {
            try
            {
                await _receiver.AbandonMessageAsync(message);
            }
            catch (ServiceBusException) { }
        }
        _locked.Clear();
        await _receiver.DisposeAsync();
    }

    // The shared complete test receives, which cannot tell a completed message from a locked one
    [Test]
    public async Task Should_complete_the_message_and_not_just_keep_it_locked()
    {
        //arrange
        await SendMessage("Testing Complete");
        var stateHandler = await GetMessageStateHandler();

        //act
        await stateHandler.CompleteAsync();

        //assert
        (await _receiver.PeekMessagesAsync(10, fromSequenceNumber: 0))
            .Should()
            .BeEmpty();
    }

    protected override async Task<List<StateHandlerCommand>> GetMessages(int count)
    {
        // Peek also returns locked messages, receiving only returns the visible ones
        var messages = await EmulatorEntities.Receive(_receiver, count);
        foreach (var message in messages)
            await _receiver.AbandonMessageAsync(message);
        return messages.Select(Deserialize).ToList();
    }

    protected override async Task<List<StateHandlerCommand>> GetDeadLetterMessages(int count)
    {
        await using var receiver = _client.CreateReceiver(
            Queue,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter }
        );
        var messages = await receiver.PeekMessagesAsync(count);
        return messages.Select(Deserialize).ToList();
    }

    protected override async Task SendMessage(string message)
    {
        await using var sender = _client.CreateSender(Queue);
        await sender.SendMessageAsync(
            new ServiceBusMessage(
                new NewtonsoftSerializer().Serialize(new StateHandlerCommand { Message = message })
            )
        );
    }

    protected override async Task<
        IMessageStateHandler<StateHandlerCommand>
    > GetMessageStateHandler()
    {
        var message = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
        _locked.Add(message);
        return new ServiceBusMessageStateHandler<StateHandlerCommand>(
            new ProcessMessageEventArgs(message, _receiver, CancellationToken.None),
            new NewtonsoftSerializer(),
            1,
            null!
        );
    }

    private static StateHandlerCommand Deserialize(ServiceBusReceivedMessage message) =>
        new NewtonsoftSerializer().Deserialize<StateHandlerCommand>(message.Body.ToMemory());
}
