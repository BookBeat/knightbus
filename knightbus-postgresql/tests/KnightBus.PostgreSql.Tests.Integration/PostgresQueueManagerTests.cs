using KnightBus.Core;
using KnightBus.Core.Management;
using KnightBus.Newtonsoft;
using KnightBus.PostgreSql.Management;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

[TestFixture]
public class PostgresQueueManagerTests : QueueManagerTests<PostgresTestCommand>
{
    private PostgresBus _bus;
    private PostgresQueueClient<PostgresTestCommand> _postgresQueueClient;
    private PostgresManagementClient _postgresManagementClient;

    public override async Task Setup()
    {
        _postgresManagementClient = new PostgresManagementClient(PostgresTestBase.TestNpgsqlDataSource, new NewtonsoftSerializer());
        _postgresQueueClient = new PostgresQueueClient<PostgresTestCommand>(PostgresTestBase.TestNpgsqlDataSource, new NewtonsoftSerializer());
        QueueManager = new PostgresQueueManager(_postgresManagementClient, new NewtonsoftSerializer());
        QueueType = QueueType.Queue;
        _bus = new PostgresBus(PostgresTestBase.TestNpgsqlDataSource,
            new PostgresConfiguration { MessageSerializer = new NewtonsoftSerializer() });

        await CleanUpTestData();
    }

    [OneTimeTearDown]
    public async Task CleanUpAfterTests() => await CleanUpTestData();

    public override async Task<string> CreateQueue()
    {
        var queueName = Guid.NewGuid().ToString("N");
        await QueueInitializer.InitQueue(PostgresQueueName.Create(queueName), PostgresTestBase.TestNpgsqlDataSource);
        return queueName;
    }

    public override async Task<string> SendMessage(string message)
    {
        await QueueInitializer.InitQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()),
            PostgresTestBase.TestNpgsqlDataSource);
        await _bus.SendAsync(new PostgresTestCommand(message));
        return AutoMessageMapper.GetQueueName<PostgresTestCommand>();
    }

    public override async Task<IMessageStateHandler<PostgresTestCommand>> GetMessageStateHandler(string queueName)
    {
        var message = await _postgresQueueClient.GetMessagesAsync(1, 10);
        return new PostgresMessageStateHandler<PostgresTestCommand>(
            PostgresTestBase.TestNpgsqlDataSource, message.First(), 5, new NewtonsoftSerializer(), null!);
    }

    private async Task CleanUpTestData()
    {
        var queues = await QueueManager.List(default);
        await QueueManager.Delete(AutoMessageMapper.GetQueueName<PostgresTestCommand>(), default);
        foreach (var queue in queues)
        {
            await QueueManager.Delete(queue.Name, default);
        }
    }
}
