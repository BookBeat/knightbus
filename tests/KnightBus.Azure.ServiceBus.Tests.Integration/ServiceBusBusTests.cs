using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using NUnit.Framework;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

[TestFixture]
public class ServiceBusBusTests
{
    private static readonly string Queue = AutoMessageMapper.GetQueueName<BusCommand>();
    private static readonly string Topic = AutoMessageMapper.GetQueueName<BusEvent>();
    private const string Subscription = "bus-sub";

    // A received message left locked survives the next purge and comes back when its lock expires
    private static readonly ServiceBusReceiverOptions ReceiveAndDelete = new()
    {
        ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
    };
    private ServiceBusClient _client = null!;
    private ClientFactory _clientFactory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _client = new ServiceBusClient(ServiceBusSetup.ConnectionString);
        _clientFactory = new ClientFactory(ServiceBusSetup.ConnectionString);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _clientFactory.DisposeAsync();
        await _client.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        await EmulatorEntities.PurgeQueue(_client, Queue);
        await EmulatorEntities.PurgeSubscription(_client, Topic, Subscription);
    }

    [Test]
    public async Task Should_send_a_command_to_its_queue()
    {
        //act
        await CreateBus().SendAsync(new BusCommand { Message = "one" });

        //assert
        var messages = await ReceiveCommands(1);
        messages.Select(m => m.Message).Should().Equal("one");
    }

    [Test]
    public async Task Should_send_a_batch_of_commands_to_its_queue()
    {
        //arrange
        var commands = Enumerable.Range(0, 50).Select(i => new BusCommand { Message = $"{i}" });

        //act
        await CreateBus().SendAsync(commands);

        //assert
        var messages = await ReceiveCommands(50);
        messages.Select(m => m.Message).Should().BeEquivalentTo(commands.Select(c => c.Message));
    }

    [Test]
    public async Task Should_split_a_batch_that_does_not_fit_in_one_message_batch()
    {
        //arrange: 100 KB each, so the 256 KB batch limit forces several sends
        var commands = Enumerable
            .Range(0, 5)
            .Select(i => new BusCommand { Message = new string((char)('a' + i), 100_000) })
            .ToList();

        //act
        await CreateBus().SendAsync(commands);

        //assert
        var messages = await ReceiveCommands(5);
        messages.Select(m => m.Message).Should().BeEquivalentTo(commands.Select(c => c.Message));
    }

    [Test]
    public async Task Should_throw_when_a_single_message_is_too_large_for_a_batch()
    {
        //arrange
        var commands = new[] { new BusCommand { Message = new string('a', 300_000) } };

        //act
        var act = () => CreateBus().SendAsync(commands);

        //assert
        await act.Should().ThrowAsync<ServiceBusMessageTooLargeException>();
    }

    [Test]
    public async Task Should_publish_an_event_to_its_subscriptions()
    {
        //act
        await CreateBus().PublishEventAsync(new BusEvent { Message = "event" });

        //assert
        var messages = await ReceiveEvents(1);
        messages.Select(m => m.Message).Should().Equal("event");
    }

    [Test]
    public async Task Should_publish_a_batch_of_events_to_its_subscriptions()
    {
        //arrange
        var events = Enumerable.Range(0, 20).Select(i => new BusEvent { Message = $"{i}" });

        //act
        await CreateBus().PublishEventsAsync(events);

        //assert
        var messages = await ReceiveEvents(20);
        messages.Select(m => m.Message).Should().BeEquivalentTo(events.Select(e => e.Message));
    }

    [Test]
    public async Task Should_schedule_a_command_for_later_delivery()
    {
        //act
        var sequenceNumber = await CreateBus()
            .ScheduleAsync(new BusCommand { Message = "later" }, TimeSpan.FromSeconds(3));

        //assert: hidden until the scheduled time, then delivered
        var scheduled = await PeekScheduled();
        scheduled.Select(m => m.SequenceNumber).Should().Equal(sequenceNumber);
        scheduled[0]
            .ScheduledEnqueueTime.Should()
            .BeCloseTo(DateTimeOffset.UtcNow.AddSeconds(3), TimeSpan.FromSeconds(2));
        (await ReceiveCommands(1, TimeSpan.FromMilliseconds(500))).Should().BeEmpty();

        var messages = await ReceiveCommands(1, TimeSpan.FromSeconds(10));
        messages.Select(m => m.Message).Should().Equal("later");
    }

    [Test]
    public async Task Should_schedule_a_batch_of_commands_and_return_their_sequence_numbers_in_order()
    {
        //arrange
        var commands = Enumerable
            .Range(0, 3)
            .Select(i => new BusCommand { Message = $"{i}" })
            .ToList();

        //act
        var sequenceNumbers = await CreateBus().ScheduleAsync(commands, TimeSpan.FromMinutes(5));

        //assert
        var scheduled = await PeekScheduled();
        scheduled
            .Select(m => (m.SequenceNumber, Deserialize<BusCommand>(m).Message))
            .Should()
            .BeEquivalentTo(sequenceNumbers.Zip(commands.Select(c => c.Message)));
    }

    [Test]
    public async Task Should_return_no_sequence_numbers_when_scheduling_an_empty_batch()
    {
        //act
        var sequenceNumbers = await CreateBus()
            .ScheduleAsync(new List<BusCommand>(), TimeSpan.FromMinutes(5));

        //assert
        sequenceNumbers.Should().BeEmpty();
    }

    [Test]
    public async Task Should_cancel_a_scheduled_command()
    {
        //arrange
        var bus = CreateBus();
        var sequenceNumber = await bus.ScheduleAsync(
            new BusCommand { Message = "cancelled" },
            TimeSpan.FromMinutes(5)
        );

        //act
        await bus.CancelScheduledAsync<BusCommand>(sequenceNumber);

        //assert
        (await PeekScheduled())
            .Should()
            .BeEmpty();
    }

    [Test]
    public async Task Should_add_pre_processor_properties_to_the_application_properties()
    {
        //arrange
        var bus = CreateBus(new StaticPropertiesPreProcessor());

        //act
        await bus.SendAsync(new BusCommand { Message = "with properties" });

        //assert
        await using var receiver = _client.CreateReceiver(Queue, ReceiveAndDelete);
        var message = (await EmulatorEntities.Receive(receiver, 1)).Single();
        message.ApplicationProperties.Should().Contain("Origin", "bus-tests");
        message.ContentType.Should().Be(new NewtonsoftSerializer().ContentType);
    }

    private ServiceBus CreateBus(params IMessagePreProcessor[] preProcessors) =>
        new(
            new ServiceBusConfiguration(ServiceBusSetup.ConnectionString),
            _clientFactory,
            preProcessors
        );

    private async Task<List<BusCommand>> ReceiveCommands(int count, TimeSpan? maxWait = null)
    {
        await using var receiver = _client.CreateReceiver(Queue, ReceiveAndDelete);
        var messages = await EmulatorEntities.Receive(
            receiver,
            count,
            maxWait ?? TimeSpan.FromSeconds(5)
        );
        return messages.Select(Deserialize<BusCommand>).ToList();
    }

    private async Task<List<BusEvent>> ReceiveEvents(int count)
    {
        await using var receiver = _client.CreateReceiver(Topic, Subscription, ReceiveAndDelete);
        var messages = await EmulatorEntities.Receive(receiver, count, TimeSpan.FromSeconds(5));
        return messages.Select(Deserialize<BusEvent>).ToList();
    }

    private async Task<List<ServiceBusReceivedMessage>> PeekScheduled()
    {
        await using var receiver = _client.CreateReceiver(Queue);
        var messages = await receiver.PeekMessagesAsync(100, fromSequenceNumber: 0);
        return messages.Where(m => m.State == ServiceBusMessageState.Scheduled).ToList();
    }

    private static T Deserialize<T>(ServiceBusReceivedMessage message) =>
        new NewtonsoftSerializer().Deserialize<T>(message.Body.ToMemory());

    private class StaticPropertiesPreProcessor : IMessagePreProcessor
    {
        public Task<IDictionary<string, object>> PreProcess<T>(
            T message,
            CancellationToken cancellationToken
        )
            where T : IMessage =>
            Task.FromResult<IDictionary<string, object>>(
                new Dictionary<string, object> { ["Origin"] = "bus-tests" }
            );
    }
}
