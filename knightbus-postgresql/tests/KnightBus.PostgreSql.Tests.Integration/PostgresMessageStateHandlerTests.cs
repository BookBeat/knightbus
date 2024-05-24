using KnightBus.Core;
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
            PostgresSetup.DataSource,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() });
        _postgresQueueClient = new PostgresQueueClient<PostgresTestCommand>(
            PostgresSetup.DataSource, new MicrosoftJsonSerializer());
        _bus = new PostgresBus(
            PostgresSetup.DataSource,
            new PostgresConfiguration { MessageSerializer = new MicrosoftJsonSerializer() });

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
        var messages = _postgresQueueClient.GetMessagesAsync(count, 0, default);
        var result = new List<PostgresTestCommand>();
        await foreach (var m in messages)
        {
            result.Add(m.Message);
        }

        return result;
    }

    protected override async Task<List<PostgresTestCommand>> GetDeadLetterMessages(int count)
    {
        var messages = _postgresQueueClient.PeekDeadLetterMessagesAsync(count, default);
        var result = new List<PostgresTestCommand>();
        await foreach (var m in messages)
        {
            result.Add(m.Message);
        }

        return result;
    }

    protected override async Task SendMessage(string message)
    {
        await QueueInitializer.InitQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()),
            PostgresSetup.DataSource);
        await _bus.SendAsync(new PostgresTestCommand(message), default);
    }

    protected override async Task<IMessageStateHandler<PostgresTestCommand>> GetMessageStateHandler()
    {
        var messages = _postgresQueueClient.GetMessagesAsync(1, 5, default);
        var result = new List<PostgresMessage<PostgresTestCommand>>();
        await foreach (var m in messages)
        {
            result.Add(m);
        }
        

        return new PostgresMessageStateHandler<PostgresTestCommand>(PostgresSetup.DataSource, _postgresQueueClient,
            result.First(), 5, new MicrosoftJsonSerializer(), null!);
    }
}
