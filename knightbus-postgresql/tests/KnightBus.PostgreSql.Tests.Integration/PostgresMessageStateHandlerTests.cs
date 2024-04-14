using KnightBus.Core;
using KnightBus.Newtonsoft;
using KnightBus.Shared.Tests.Integration;
using Newtonsoft.Json;

namespace KnightBus.PostgreSql.Tests.Integration;

public class PostgresMessageStateHandlerTests : MessageStateHandlerTests<PostgresTestCommand>
{
    private PostgresBus _bus;
    private PostgresQueueClient<PostgresTestCommand> _postgresQueueClient;
    private PostgresManagementClient _postgresManagementClient;
    public override async Task Setup()
    {
        _postgresManagementClient = new PostgresManagementClient(PostgresTestBase.TestNpgsqlDataSource, new NewtonsoftSerializer());
        _postgresQueueClient = new PostgresQueueClient<PostgresTestCommand>(PostgresTestBase.TestNpgsqlDataSource, new NewtonsoftSerializer());
        var qm = new PostgresQueueManager(_postgresManagementClient, new NewtonsoftSerializer());
        _bus = new PostgresBus(PostgresTestBase.TestNpgsqlDataSource,
            new PostgresConfiguration { MessageSerializer = new NewtonsoftSerializer() });
        await qm.Delete(AutoMessageMapper.GetQueueName<PostgresTestCommand>(), default);
    }

    protected override async Task<List<PostgresTestCommand>> GetMessages(int count)
    {
        var messages = await _postgresManagementClient.PeekMessagesAsync(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()), count, default);
        return messages.Select(m =>
            JsonConvert.DeserializeObject<PostgresTestCommand>(JsonConvert.SerializeObject(m.Message))).ToList()!;
    }

    protected override async Task<List<PostgresTestCommand>> GetDeadLetterMessages(int count)
    {
        var messages = await _postgresManagementClient.PeekDeadLettersAsync(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()), count, default);
        return messages.Select(m =>
            JsonConvert.DeserializeObject<PostgresTestCommand>(JsonConvert.SerializeObject(m.Message))).ToList()!;
    }

    protected override async Task SendMessage(string message)
    {
        await _postgresManagementClient.InitQueue(
            PostgresQueueName.Create(AutoMessageMapper.GetQueueName<PostgresTestCommand>()));
        await _bus.SendAsync(new PostgresTestCommand(message));
    }

    protected override async Task<IMessageStateHandler<PostgresTestCommand>> GetMessageStateHandler()
    {
        var m = await _postgresQueueClient.GetMessagesAsync(1, 0);
        return new PostgresMessageStateHandler<PostgresTestCommand>(PostgresTestBase.TestNpgsqlDataSource,
            m.First(), 5, new NewtonsoftSerializer(), null!);
    }
}
