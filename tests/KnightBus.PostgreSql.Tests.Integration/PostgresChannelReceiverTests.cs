using KnightBus.Core;
using KnightBus.PostgreSql.Management;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

[TestFixture]
public class PostgresChannelReceiverTests : ChannelReceiverTests
{
    private static readonly PostgresQueueName Queue = PostgresQueueName.Create(
        AutoMessageMapper.GetQueueName<PostgresTestCommand>()
    );
    private readonly PostgresConfiguration _configuration = new()
    {
        MessageSerializer = new MicrosoftJsonSerializer(),
    };
    private PostgresBus _bus = null!;

    public override async Task Setup()
    {
        await DeleteQueue();
        await QueueInitializer.InitQueue(Queue, PostgresSetup.DataSource);
        _bus = new PostgresBus(PostgresSetup.DataSource, _configuration, []);
    }

    //The other suites use the same queue
    [OneTimeTearDown]
    public Task DeleteQueue() =>
        new PostgresManagementClient(PostgresSetup.DataSource, _configuration).DeleteQueue(
            Queue,
            default
        );

    protected override Task SendMessage(string message) =>
        _bus.SendAsync(new PostgresTestCommand(message), default);

    protected override IChannelReceiver CreateReceiver(IMessageProcessor processor) =>
        new PostgresChannelReceiver<PostgresTestCommand>(
            PostgresSetup.DataSource,
            processor,
            Settings,
            HostConfiguration,
            new MicrosoftJsonSerializer(),
            _configuration
        );
}

[TestFixture]
public class PostgresSubscriptionChannelReceiverTests : ChannelReceiverTests
{
    private static readonly PostgresQueueName Topic = PostgresQueueName.Create(
        AutoMessageMapper.GetQueueName<PostgresTestEvent>()
    );
    private readonly PostgresTestEventSubscription _subscription = new();
    private readonly PostgresConfiguration _configuration = new()
    {
        MessageSerializer = new MicrosoftJsonSerializer(),
    };
    private PostgresBus _bus = null!;

    public override async Task Setup()
    {
        //Deleting needs the topic, so it is created first
        await QueueInitializer.InitSubscription(Topic, Subscription, PostgresSetup.DataSource);
        await DeleteSubscription();
        await QueueInitializer.InitSubscription(Topic, Subscription, PostgresSetup.DataSource);
        _bus = new PostgresBus(PostgresSetup.DataSource, _configuration, []);
    }

    //The other suites use the same subscription
    [OneTimeTearDown]
    public Task DeleteSubscription() =>
        new PostgresManagementClient(PostgresSetup.DataSource, _configuration).DeleteSubscription(
            Topic,
            Subscription,
            default
        );

    private PostgresQueueName Subscription => PostgresQueueName.Create(_subscription.Name);

    protected override Task SendMessage(string message) =>
        _bus.PublishAsync(new PostgresTestEvent(message), default);

    protected override IChannelReceiver CreateReceiver(IMessageProcessor processor) =>
        new PostgresSubscriptionChannelReceiver<PostgresTestEvent>(
            PostgresSetup.DataSource,
            processor,
            _subscription,
            Settings,
            HostConfiguration,
            new MicrosoftJsonSerializer(),
            _configuration
        );
}
