using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using KnightBus.Azure.ServiceBus.Management;
using KnightBus.Core.Management;
using NUnit.Framework;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

// The shared QueueManagerTests create and list queues through the administration client, which
// shares ConnectionString with the data plane and so cannot reach the emulator's admin port
[TestFixture]
public class ServiceBusQueueManagerTests
{
    private const string Queue = "queue-manager";
    private ServiceBusClient _client = null!;
    private ServiceBusQueueManager _queueManager = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _client = new ServiceBusClient(ServiceBusSetup.ConnectionString);
        _queueManager = new ServiceBusQueueManager(
            new ServiceBusConfiguration(ServiceBusSetup.ConnectionString)
        );
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _queueManager.DisposeAsync();
        await _client.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp() => await EmulatorEntities.PurgeQueue(_client, Queue);

    [Test]
    public void Should_be_a_queue_manager() => _queueManager.QueueType.Should().Be(QueueType.Queue);

    [Test]
    public async Task Should_send_a_message_and_peek_it_without_removing_it()
    {
        //act
        await _queueManager.SendMessage(Queue, "{\"Message\":\"peek\"}", CancellationToken.None);

        //assert
        var messages = await _queueManager.Peek(Queue, 10, CancellationToken.None);
        messages.Select(m => m.Body).Should().Equal("{\"Message\":\"peek\"}");
        (await _queueManager.Peek(Queue, 10, CancellationToken.None)).Should().HaveCount(1);
    }

    [Test]
    public async Task Should_send_several_messages()
    {
        //act
        await _queueManager.SendMessages(Queue, ["{\"n\":1}", "{\"n\":2}"], CancellationToken.None);

        //assert
        var messages = await _queueManager.Peek(Queue, 10, CancellationToken.None);
        messages.Select(m => m.Body).Should().BeEquivalentTo("{\"n\":1}", "{\"n\":2}");
    }

    [Test]
    public async Task Should_keep_scheduled_messages_out_of_peek_and_list_them_in_peek_scheduled()
    {
        //arrange
        await SendAndSchedule("{\"n\":\"now\"}", "{\"n\":\"later\"}");

        //act
        var active = await _queueManager.Peek(Queue, 10, CancellationToken.None);
        var scheduled = await _queueManager.PeekScheduled(Queue, 10, CancellationToken.None);

        //assert
        active.Select(m => m.Body).Should().Equal("{\"n\":\"now\"}");
        scheduled.Select(m => m.Body).Should().Equal("{\"n\":\"later\"}");
        scheduled[0].ScheduledTime.Should().BeAfter(DateTimeOffset.UtcNow);
        scheduled[0].SequenceNumber.Should().NotBeNull();
    }

    [Test]
    public async Task Should_cancel_a_scheduled_message()
    {
        //arrange
        await SendAndSchedule(null, "{\"n\":\"later\"}");
        var scheduled = (await _queueManager.PeekScheduled(Queue, 10, CancellationToken.None))[0];

        //act
        await _queueManager.CancelScheduledMessage(
            Queue,
            scheduled.SequenceNumber!.Value,
            CancellationToken.None
        );

        //assert
        (await _queueManager.PeekScheduled(Queue, 10, CancellationToken.None))
            .Should()
            .BeEmpty();
    }

    [Test]
    public async Task Should_peek_a_dead_letter_with_its_error_without_removing_it()
    {
        //arrange
        await DeadLetter("{\"n\":1}");

        //act
        var messages = await _queueManager.PeekDeadLetter(Queue, 10, CancellationToken.None);

        //assert
        messages.Should().ContainSingle();
        messages[0].Body.Should().Be("{\"n\":1}");
        messages[0].Error.Should().Be("boom");
        (await _queueManager.PeekDeadLetter(Queue, 10, CancellationToken.None))
            .Should()
            .HaveCount(1);
    }

    [Test]
    public async Task Should_read_dead_letters_and_remove_them()
    {
        //arrange
        await DeadLetter("{\"n\":1}");
        await DeadLetter("{\"n\":2}");

        //act: ReadDeadLetter receives without a max wait, so a limit above the count would block for the SDK's 60 s default
        var messages = await _queueManager.ReadDeadLetter(Queue, 2, CancellationToken.None);

        //assert
        messages.Select(m => m.Body).Should().BeEquivalentTo("{\"n\":1}", "{\"n\":2}");
        (await _queueManager.PeekDeadLetter(Queue, 10, CancellationToken.None)).Should().BeEmpty();
    }

    [Test]
    public async Task Should_move_dead_letters_back_to_the_queue()
    {
        //arrange
        await DeadLetter("{\"n\":1}");

        //act
        var moved = await _queueManager.MoveDeadLetters(Queue, 1, CancellationToken.None);

        //assert
        moved.Should().Be(1);
        (await _queueManager.PeekDeadLetter(Queue, 10, CancellationToken.None)).Should().BeEmpty();
        var messages = await _queueManager.Peek(Queue, 10, CancellationToken.None);
        messages.Select(m => m.Body).Should().Equal("{\"n\":1}");
    }

    [Test]
    public async Task Should_list_and_get_queues_through_the_administration_endpoint()
    {
        //arrange
        await using var adminManager = new ServiceBusQueueManager(
            new ServiceBusConfiguration(ServiceBusSetup.AdministrationConnectionString)
        );

        //act
        var queues = await adminManager.List(CancellationToken.None);
        var queue = await adminManager.Get(Queue, CancellationToken.None);

        //assert: the emulator does not track runtime properties (counts, size), only the entities are checked
        //https://github.com/Azure/azure-service-bus-emulator-installer/issues/17#issuecomment-3772045623
        queues.Select(q => q.Name).Should().Contain(Queue);
        queue.Name.Should().Be(Queue);
        queue.Type.Should().Be(QueueType.Queue);
    }

    private async Task SendAndSchedule(string? now, string later)
    {
        await using var sender = _client.CreateSender(Queue);
        if (now != null)
            await sender.SendMessageAsync(new ServiceBusMessage(now));
        await sender.ScheduleMessageAsync(
            new ServiceBusMessage(later),
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
    }

    private async Task DeadLetter(string body)
    {
        await using var sender = _client.CreateSender(Queue);
        await using var receiver = _client.CreateReceiver(Queue);
        await EmulatorEntities.SendToDeadLetter(sender, receiver, body);
    }
}
