using KnightBus.Core;
using KnightBus.Newtonsoft;
using KnightBus.PostgreSql.Management;
using KnightBus.Shared.Tests.Integration;
using NUnit.Framework;

namespace KnightBus.PostgreSql.Tests.Integration;

public class PostgresMessageStateHandlerTests : MessageStateHandlerTests<PostgresTestCommand>
{
    private PostgresBus _bus;
    private PostgresQueueClient<PostgresTestCommand> _postgresQueueClient;
    private PostgresManagementClient _postgresManagementClient;

    public override async Task Setup()
    {
        _postgresManagementClient = new PostgresManagementClient(
            PostgresTestBase.TestNpgsqlDataSource, new NewtonsoftSerializer());
        _postgresQueueClient = new PostgresQueueClient<PostgresTestCommand>(
            PostgresTestBase.TestNpgsqlDataSource, new NewtonsoftSerializer());
        _bus = new PostgresBus(
            PostgresTestBase.TestNpgsqlDataSource,
            new PostgresConfiguration { MessageSerializer = new NewtonsoftSerializer() });

        await _postgresManagementClient.DeleteQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()), default);
    }

    [OneTimeTearDown]
    public async Task CleanUpAfterTests()
    {
        await _postgresManagementClient.DeleteQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()), default);
    }

    protected override async Task<List<PostgresTestCommand>> GetMessages(int count)
    {
        var messages = await _postgresQueueClient.GetMessagesAsync(count, 0);
        return messages.Select(m => m.Message).ToList();
    }

    protected override async Task<List<PostgresTestCommand>> GetDeadLetterMessages(int count)
    {
        var messages = await _postgresQueueClient.PeekDeadLetterMessagesAsync(count, default);
        return messages.Select(m => m.Message).ToList();
    }

    protected override async Task SendMessage(string message)
    {
        await QueueInitializer.InitQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()),
            PostgresTestBase.TestNpgsqlDataSource);
        await _bus.SendAsync(new PostgresTestCommand(message), default);
    }

    protected override async Task<IMessageStateHandler<PostgresTestCommand>> GetMessageStateHandler()
    {
        var m = await _postgresQueueClient.GetMessagesAsync(1, 0);
        return new PostgresMessageStateHandler<PostgresTestCommand>(PostgresTestBase.TestNpgsqlDataSource,
            m.First(), 5, new NewtonsoftSerializer(), null!);
    }
}
