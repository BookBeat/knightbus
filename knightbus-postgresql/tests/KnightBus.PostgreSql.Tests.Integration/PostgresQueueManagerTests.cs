using KnightBus.Core;
using KnightBus.Core.Management;
using KnightBus.Messages;
using KnightBus.Newtonsoft;
using KnightBus.PostgreSql.Messages;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

[TestFixture]
public class PostgresQueueManagerTests : QueueManagerTests<PostgresTestCommand>
{
    private PostgresBus _bus;
    private PostgresQueueClient<PostgresTestCommand> _postgresQueueClient;

    public override async Task Setup()
    {
        var managementClient = new PostgresManagementClient(PostgresTestBase.TestNpgsqlDataSource, new NewtonsoftSerializer());
        _postgresQueueClient = new PostgresQueueClient<PostgresTestCommand>(PostgresTestBase.TestNpgsqlDataSource, new NewtonsoftSerializer());
        QueueManager = new PostgresQueueManager(managementClient, new NewtonsoftSerializer());
        QueueType = QueueType.Queue;
        _bus = new PostgresBus(PostgresTestBase.TestNpgsqlDataSource,
            new PostgresConfiguration { MessageSerializer = new NewtonsoftSerializer() });

        var queues = await QueueManager.List(CancellationToken.None);
        await QueueManager.Delete("test", CancellationToken.None);
        foreach (var queue in queues)
        {
            await QueueManager.Delete(queue.Name, CancellationToken.None);
        }
    }

    public override async Task<string> CreateQueue()
    {
        var queueName = Guid.NewGuid().ToString("N");
        await _postgresQueueClient.InitQueue(queueName);
        return queueName;
    }

    public override async Task<string> SendMessage(string message)
    {
        await _postgresQueueClient.InitQueue();
        await _bus.SendAsync(new PostgresTestCommand(message));
        return AutoMessageMapper.GetQueueName<PostgresTestCommand>();
    }

    public override async Task<IMessageStateHandler<PostgresTestCommand>> GetMessageStateHandler(string queueName)
    {
        var message = await _postgresQueueClient.GetMessagesAsync(1, 10);
        return new PostgresMessageStateHandler<PostgresTestCommand>(
            PostgresTestBase.TestNpgsqlDataSource, message.First(), 5, new NewtonsoftSerializer(), null!);
    }
}

public class PostgresTestCommand : IPostgresCommand
{
    public string Value { get; set; }

    public PostgresTestCommand(string value)
    {
        Value = value;
    }
}

public class PostgresTestCommandMapping : IMessageMapping<PostgresTestCommand>
{
    public string QueueName => "test";
}
