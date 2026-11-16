using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Azure.ServiceBus.Management;
using KnightBus.Azure.ServiceBus.Messages;
using KnightBus.Core.Management;
using KnightBus.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

[TestFixture]
public class SubscriptionForwardingTests
{
    private const string Topic = "forward-events";
    private const string Subscription = "forward-sub";
    private const string ManagerSubscription = "forward-manager";
    private const string Sibling = "forward-other";
    private static readonly string Queue = $"{Topic}.{Subscription}";
    private static readonly string ManagerQueue = $"{Topic}.{ManagerSubscription}";

    private ServiceBusClient _client = null!;
    private ServiceBusAdministrationClient _admin = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _client = new ServiceBusClient(ServiceBusSetup.ConnectionString);
        _admin = new ServiceBusAdministrationClient(ServiceBusSetup.AdministrationConnectionString);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _client.DisposeAsync();

    [SetUp]
    public async Task SetUp()
    {
        await StopForwarding(Subscription);
        await StopForwarding(ManagerSubscription);
        await EmulatorEntities.PurgeSubscription(_client, Topic, Subscription);
        await EmulatorEntities.PurgeSubscription(_client, Topic, ManagerSubscription);
        await EmulatorEntities.PurgeSubscription(_client, Topic, Sibling);
        await EmulatorEntities.PurgeQueue(_client, Queue);
        await EmulatorEntities.PurgeQueue(_client, ManagerQueue);
    }

    [Test]
    public async Task Should_point_an_existing_subscription_at_its_queue()
    {
        //act
        await Ensure(Subscription, Queue);

        //assert
        var properties = await _admin.GetSubscriptionAsync(Topic, Subscription);
        ServiceBusEntityName.FromForwardTo(properties.Value.ForwardTo).Should().Be(Queue);
    }

    [Test]
    public async Task Should_leave_a_subscription_alone_that_already_forwards_to_its_queue()
    {
        //arrange
        await Ensure(Subscription, Queue);
        var before = (await _admin.GetSubscriptionAsync(Topic, Subscription)).Value;

        //act
        await Ensure(Subscription, Queue);

        //assert
        var after = (await _admin.GetSubscriptionAsync(Topic, Subscription)).Value;
        after.ForwardTo.Should().Be(before.ForwardTo);
    }

    [Test]
    public async Task Should_deliver_published_events_to_the_queue_and_still_to_other_subscriptions()
    {
        //arrange
        await Ensure(Subscription, Queue);
        await using var sender = _client.CreateSender(Topic);

        //act
        await sender.SendMessageAsync(new ServiceBusMessage("{\"n\":1}"));

        //assert
        await using var queueReceiver = _client.CreateReceiver(Queue);
        var forwarded = await EmulatorEntities.Eventually(
            () => queueReceiver.PeekMessagesAsync(10),
            m => m.Count > 0
        );
        forwarded.Select(m => m.Body.ToString()).Should().Equal("{\"n\":1}");

        await using var siblingReceiver = _client.CreateReceiver(Topic, Sibling);
        var sibling = await EmulatorEntities.Eventually(
            () => siblingReceiver.PeekMessagesAsync(10),
            m => m.Count > 0
        );
        sibling.Should().ContainSingle();
    }

    [Test]
    public async Task Should_report_the_queue_a_subscription_forwards_to()
    {
        //arrange
        await Ensure(ManagerSubscription, ManagerQueue);
        var manager = new ServiceBusSubscriptionManager(Topic, _client, _admin);

        //act
        var listed = (await manager.List(CancellationToken.None))
            .OfType<SubscriptionQueueProperties>()
            .ToDictionary(s => s.Name);
        var fetched = (SubscriptionQueueProperties)
            await manager.Get(ManagerSubscription, CancellationToken.None);

        //assert
        listed[ManagerSubscription].ForwardTo.Should().Be(ManagerQueue);
        listed[Sibling].ForwardTo.Should().BeNull();
        fetched.ForwardTo.Should().Be(ManagerQueue);
    }

    [Test]
    public async Task Should_move_dead_letters_of_a_forwarded_subscription_to_its_queue_only()
    {
        //arrange: a dead letter left from before forwarding was turned on
        await using (var sender = _client.CreateSender(Topic))
        await using (var receiver = _client.CreateReceiver(Topic, ManagerSubscription))
            await EmulatorEntities.SendToDeadLetter(sender, receiver, "{\"n\":2}");
        await EmulatorEntities.PurgeSubscription(_client, Topic, Sibling);
        await Ensure(ManagerSubscription, ManagerQueue);
        var manager = new ServiceBusSubscriptionManager(Topic, _client, _admin);

        //act
        var moved = await manager.MoveDeadLetters(ManagerSubscription, 10, CancellationToken.None);

        //assert
        moved.Should().Be(1);
        await using var queueReceiver = _client.CreateReceiver(ManagerQueue);
        var requeued = await EmulatorEntities.Eventually(
            () => queueReceiver.PeekMessagesAsync(10),
            m => m.Count > 0
        );
        requeued.Select(m => m.Body.ToString()).Should().Equal("{\"n\":2}");
        await using var siblingReceiver = _client.CreateReceiver(Topic, Sibling);
        (await siblingReceiver.PeekMessagesAsync(10)).Should().BeEmpty();
    }

    [Test]
    public void Should_not_forward_by_default()
    {
        var queue = SubscriptionForwarding.GetForwardingQueue(
            new ServiceBusConfiguration(),
            Topic,
            new PlainSubscription()
        );

        queue.Should().BeNull();
    }

    [Test]
    public void Should_forward_every_subscription_when_enabled_on_the_host()
    {
        var configuration = new ServiceBusConfiguration();
        configuration.SubscriptionForwarding.Enabled = true;

        var queue = SubscriptionForwarding.GetForwardingQueue(
            configuration,
            Topic,
            new PlainSubscription()
        );

        queue.Should().Be("forward-events.plain");
    }

    [Test]
    public void Should_let_a_subscription_override_the_host()
    {
        var enabled = new ServiceBusConfiguration();
        enabled.SubscriptionForwarding.Enabled = true;
        var named = new ServiceBusConfiguration();
        named.SubscriptionForwarding.QueueName = (topic, subscription) =>
            $"{subscription}-from-{topic}";

        SubscriptionForwarding
            .GetForwardingQueue(enabled, Topic, new ForwardingSubscription(false))
            .Should()
            .BeNull();
        SubscriptionForwarding
            .GetForwardingQueue(named, Topic, new ForwardingSubscription(true))
            .Should()
            .Be("opted-from-forward-events");
    }

    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("my-queue", "my-queue")]
    [TestCase("sb://ns.servicebus.windows.net/my.queue", "my.queue")]
    [TestCase("https://ns.servicebus.windows.net/my.queue/", "my.queue")]
    public void Should_read_the_entity_name_of_a_forwarding_target(
        string? forwardTo,
        string? expected
    ) => ServiceBusEntityName.FromForwardTo(forwardTo).Should().Be(expected);

    private Task Ensure(string subscription, string queue) =>
        SubscriptionForwarding.EnsureAsync(
            _admin,
            Topic,
            subscription,
            queue,
            new ServiceBusCreationOptions(),
            NullLogger.Instance,
            CancellationToken.None
        );

    private async Task StopForwarding(string subscription)
    {
        var properties = (await _admin.GetSubscriptionAsync(Topic, subscription)).Value;
        if (string.IsNullOrEmpty(properties.ForwardTo))
            return;
        properties.ForwardTo = null;
        await _admin.UpdateSubscriptionAsync(properties);
    }

    private class PlainSubscription : IEventSubscription<HostEvent>
    {
        public string Name => "plain";
    }

    private class ForwardingSubscription(bool forward)
        : IEventSubscription<HostEvent>,
            IServiceBusForwardingSubscription
    {
        public string Name => "opted";
        public bool ForwardToQueue => forward;
    }
}
