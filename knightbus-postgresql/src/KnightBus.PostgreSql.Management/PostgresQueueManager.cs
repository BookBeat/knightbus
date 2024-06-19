using System.Text;
using KnightBus.Core.Management;
using KnightBus.Messages;

namespace KnightBus.PostgreSql.Management;

public class PostgresQueueManager : IQueueManager, IQueueMessageSender
{
    private readonly PostgresManagementClient _managementClient;
    private readonly IMessageSerializer _messageSerializer;

    public PostgresQueueManager(PostgresManagementClient managementClient, IPostgresConfiguration configuration)
    {
        _managementClient = managementClient;
        _messageSerializer = configuration.MessageSerializer;
    }

    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var queues = await _managementClient.ListQueues(ct);
        return queues.Select(q =>
            new QueueProperties(q.Name, this, true)
            {
                ActiveMessageCount = q.ActiveMessagesCount,
                DeadLetterMessageCount = q.DeadLetterMessagesCount
            });
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var queue = await _managementClient.GetQueue(PostgresQueueName.Create(path), ct);
        return new QueueProperties(queue.Name, this, true)
        {
            ActiveMessageCount = queue.ActiveMessagesCount,
            DeadLetterMessageCount = queue.DeadLetterMessagesCount,
            CreatedAt = queue.CreatedAt
        };
    }

    public Task Delete(string path, CancellationToken ct)
    {
        return _managementClient.DeleteQueue(PostgresQueueName.Create(path), ct);
    }

    public async Task<IReadOnlyList<QueueMessage>> Peek(string name, int count, CancellationToken ct)
    {
        var messages = _managementClient
            .PeekMessagesAsync(PostgresQueueName.Create(name), count, ct);

        var result = new List<QueueMessage>();
        await foreach (var m in messages)
        {
            m.Properties.TryGetValue("error_message", out var error);
            result.Add(new QueueMessage(
                Encoding.UTF8.GetString(_messageSerializer.Serialize(m.Message)),
                error ?? string.Empty,
                null,
                null,
                m.ReadCount,
                m.Id.ToString(),
                m.Properties));
        }

        return result;
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string path, int count, CancellationToken ct)
    {
        var deadLetters = _managementClient
            .PeekDeadLettersAsync(PostgresQueueName.Create(path), count, ct);

        var result = new List<QueueMessage>();
        await foreach (var m in deadLetters)
        {
            m.Properties.TryGetValue("error_message", out var error);
            result.Add(new QueueMessage(
                Encoding.UTF8.GetString(_messageSerializer.Serialize(m.Message)),
                error ?? string.Empty,
                null,
                null,
                m.ReadCount,
                m.Id.ToString(),
                m.Properties));
        }

        return result;
    }

    public async Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(string path, int count, CancellationToken ct)
    {
        var deadLetters = _managementClient
            .ReadDeadLettersAsync(PostgresQueueName.Create(path), count, ct);

        var result = new List<QueueMessage>();
        await foreach (var m in deadLetters)
        {
            m.Properties.TryGetValue("error_message", out var error);
            result.Add(new QueueMessage(
                Encoding.UTF8.GetString(_messageSerializer.Serialize(m.Message)),
                error ?? string.Empty,
                null,
                null,
                m.ReadCount,
                m.Id.ToString(),
                m.Properties));
        }

        return result;
    }

    public async Task<int> MoveDeadLetters(string path, int count, CancellationToken ct)
    {
        var result = await _managementClient.RequeueDeadLettersAsync(PostgresQueueName.Create(path), count, ct);
        return (int)result;
    }

    public async Task SendMessage(string path, string jsonBody, CancellationToken cancellationToken)
    {
        await _managementClient.SendMessage(PostgresQueueName.Create(path), jsonBody, cancellationToken);
    }

    public QueueType QueueType => QueueType.Queue;
}

