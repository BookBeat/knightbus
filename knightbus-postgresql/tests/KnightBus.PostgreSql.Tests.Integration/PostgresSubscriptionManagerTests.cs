using KnightBus.Core;
using KnightBus.Core.Management;
using KnightBus.PostgreSql.Management;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

[TestFixture]
public class PostgresSubscriptionManagerTests : QueueManagerTests<PostgresTestEvent>
{
    private PostgresBus _bus;
    private PostgresSubscriptionClient<PostgresTestEvent> _postgresQueueClient;
    private PostgresManagementClient _postgresManagementClient;
    private readonly PostgresTestEventSubscription _eventSubscription = new();

    public override async Task Setup()
    {
        _postgresManagementClient = new PostgresManagementClient(
            PostgresSetup.DataSource,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() }
        );
        _postgresQueueClient = new PostgresSubscriptionClient<PostgresTestEvent>(
            PostgresSetup.DataSource,
            new MicrosoftJsonSerializer(),
            _eventSubscription
        );
        QueueManager = new PostgresSubscriptionManager(
            AutoMessageMapper.GetQueueName<PostgresTestEvent>(),
            _postgresManagementClient,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() }
        );
        QueueType = QueueType.Subscription;
        _bus = new PostgresBus(
            PostgresSetup.DataSource,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() }
        );
        await QueueInitializer.InitSubscription(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestEvent>()),
            PostgresQueueName.Create(_eventSubscription.Name),
            PostgresSetup.DataSource
        );
        await CleanUpTestData();
    }

    [OneTimeTearDown]
    public async Task CleanUpAfterTests() => await CleanUpTestData();

    public override async Task<string> CreateQueue()
    {
        var subscription = Guid.NewGuid().ToString("N");
        await QueueInitializer.InitSubscription(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestEvent>()),
            PostgresQueueName.Create(subscription),
            PostgresSetup.DataSource
        );
        return subscription;
    }

    public override async Task<string> SendMessage(string message)
    {
        await QueueInitializer.InitSubscription(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestEvent>()),
            PostgresQueueName.Create(_eventSubscription.Name),
            PostgresSetup.DataSource
        );
        await _bus.PublishAsync(new PostgresTestEvent(message), default);
        return _eventSubscription.Name;
    }

    public override async Task<IMessageStateHandler<PostgresTestEvent>> GetMessageStateHandler(
        string queueName
    )
    {
        var messages = _postgresQueueClient.GetMessagesAsync(1, 10, default);
        var result = new List<PostgresMessage<PostgresTestEvent>>();
        await foreach (var m in messages)
        {
            result.Add(m);
        }

        return new PostgresMessageStateHandler<PostgresTestEvent>(
            PostgresSetup.DataSource,
            _postgresQueueClient,
            result.First(),
            5,
            new MicrosoftJsonSerializer(),
            null!
        );
    }

    [Test, Ignore("Not supported for PostgresBus Subscriptions")]
    public override Task ShouldMoveDeadLetterMessagesToAnotherQueue()
    {
        return Task.CompletedTask;
    }

    private async Task CleanUpTestData()
    {
        var queues = await QueueManager.List(default);
        await QueueManager.Delete(AutoMessageMapper.GetQueueName<PostgresTestEvent>(), default);
        foreach (var queue in queues)
        {
            await QueueManager.Delete(queue.Name, default);
        }
    }
}
