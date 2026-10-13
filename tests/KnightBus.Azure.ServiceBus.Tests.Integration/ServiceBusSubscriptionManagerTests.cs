using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Azure.ServiceBus.Management;
using KnightBus.Core.Management;
using NUnit.Framework;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

[TestFixture]
public class ServiceBusSubscriptionManagerTests
{
    private const string Topic = "manager-events";
    private const string Subscription = "manager-sub";
    private ServiceBusClient _client = null!;
    private ServiceBusSubscriptionManager _subscriptionManager = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _client = new ServiceBusClient(ServiceBusSetup.ConnectionString);
        _subscriptionManager = new ServiceBusSubscriptionManager(
            Topic,
            _client,
            new ServiceBusAdministrationClient(ServiceBusSetup.AdministrationConnectionString)
        );
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _client.DisposeAsync();

    [SetUp]
    public async Task SetUp() =>
        await EmulatorEntities.PurgeSubscription(_client, Topic, Subscription);

    [Test]
    public void Should_be_a_subscription_manager() =>
        _subscriptionManager.QueueType.Should().Be(QueueType.Subscription);

    [Test]
    public async Task Should_peek_a_message_without_removing_it()
    {
        //arrange
        await using var sender = _client.CreateSender(Topic);
        await sender.SendMessageAsync(new ServiceBusMessage("{\"n\":1}"));

        //act
        var messages = await EmulatorEntities.Eventually(
            () => _subscriptionManager.Peek(Subscription, 10, CancellationToken.None),
            m => m.Count > 0
        );

        //assert
        messages.Select(m => m.Body).Should().Equal("{\"n\":1}");
        (await _subscriptionManager.Peek(Subscription, 10, CancellationToken.None))
            .Should()
            .HaveCount(1);
    }

    [Test]
    public async Task Should_peek_a_dead_letter_with_its_error_without_removing_it()
    {
        //arrange
        await DeadLetter("{\"n\":1}");

        //act
        var messages = await _subscriptionManager.PeekDeadLetter(
            Subscription,
            10,
            CancellationToken.None
        );

        //assert
        messages.Should().ContainSingle();
        messages[0].Body.Should().Be("{\"n\":1}");
        messages[0].Error.Should().Be("boom");
    }

    [Test]
    public async Task Should_read_dead_letters_and_remove_them()
    {
        //arrange
        await DeadLetter("{\"n\":1}");

        //act
        var messages = await _subscriptionManager.ReadDeadLetter(
            Subscription,
            1,
            CancellationToken.None
        );

        //assert
        messages.Select(m => m.Body).Should().Equal("{\"n\":1}");
        (await _subscriptionManager.PeekDeadLetter(Subscription, 10, CancellationToken.None))
            .Should()
            .BeEmpty();
    }

    [Test]
    public async Task Should_move_dead_letters_back_to_the_subscription_through_the_topic()
    {
        //arrange
        await DeadLetter("{\"n\":1}");

        //act
        var moved = await _subscriptionManager.MoveDeadLetters(
            Subscription,
            1,
            CancellationToken.None
        );

        //assert
        moved.Should().Be(1);
        (await _subscriptionManager.PeekDeadLetter(Subscription, 10, CancellationToken.None))
            .Should()
            .BeEmpty();
        var messages = await EmulatorEntities.Eventually(
            () => _subscriptionManager.Peek(Subscription, 10, CancellationToken.None),
            m => m.Count > 0
        );
        messages.Select(m => m.Body).Should().Equal("{\"n\":1}");
    }

    [Test]
    public async Task Should_list_and_get_subscriptions_through_the_administration_endpoint()
    {
        //act
        var subscriptions = await _subscriptionManager.List(CancellationToken.None);
        var subscription = await _subscriptionManager.Get(Subscription, CancellationToken.None);

        //assert: the emulator does not track runtime properties (counts, size), only the entities are checked
        //https://github.com/Azure/azure-service-bus-emulator-installer/issues/17#issuecomment-3772045623
        subscriptions.Select(s => s.Name).Should().Equal(Subscription);
        subscription.Name.Should().Be(Subscription);
    }

    private async Task DeadLetter(string body)
    {
        await using var sender = _client.CreateSender(Topic);
        await using var receiver = _client.CreateReceiver(Topic, Subscription);
        await EmulatorEntities.SendToDeadLetter(sender, receiver, body);
    }
}
