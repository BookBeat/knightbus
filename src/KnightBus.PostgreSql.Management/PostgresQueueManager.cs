using KnightBus.Core.Management;
using KnightBus.Messages;

namespace KnightBus.PostgreSql.Management;

public class PostgresQueueManager : IQueueManager, IQueueMessageSender
{
    private readonly PostgresManagementClient _managementClient;
    private readonly IMessageSerializer _messageSerializer;

    public PostgresQueueManager(
        PostgresManagementClient managementClient,
        IPostgresConfiguration configuration
    )
    {
        _managementClient = managementClient;
        _messageSerializer = configuration.MessageSerializer;
    }

    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var queues = await _managementClient.ListQueues(ct);
        return queues.Select(q => new QueueProperties(q.Name, this, true)
        {
            ActiveMessageCount = q.ActiveMessagesCount,
            DeadLetterMessageCount = q.DeadLetterMessagesCount,
        });
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var queue = await _managementClient.GetQueue(PostgresQueueName.Create(path), ct);
        return new QueueProperties(queue.Name, this, true)
        {
            ActiveMessageCount = queue.ActiveMessagesCount,
            DeadLetterMessageCount = queue.DeadLetterMessagesCount,
            CreatedAt = queue.CreatedAt,
        };
    }

    public Task Delete(string path, CancellationToken ct)
    {
        return _managementClient.DeleteQueue(PostgresQueueName.Create(path), ct);
    }

    public async Task<IReadOnlyList<QueueMessage>> Peek(
        string name,
        int count,
        CancellationToken ct
    )
    {
        var messages = _managementClient.PeekMessagesAsync(
            PostgresQueueName.Create(name),
            count,
            ct
        );

        return await messages.ToQueueMessages(_messageSerializer).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<QueueMessage>> Receive(
        string name,
        int count,
        CancellationToken ct
    )
    {
        var messages = _managementClient.ReceiveMessagesAsync(
            PostgresQueueName.Create(name),
            count,
            ct
        );

        return await messages.ToQueueMessages(_messageSerializer).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<QueueMessage>> PeekScheduled(
        string name,
        int count,
        CancellationToken ct
    )
    {
        throw new NotSupportedException();
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(
        string path,
        int count,
        CancellationToken ct
    )
    {
        var deadLetters = _managementClient.PeekDeadLettersAsync(
            PostgresQueueName.Create(path),
            count,
            ct
        );

        return await deadLetters.ToQueueMessages(_messageSerializer).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(
        string path,
        int count,
        CancellationToken ct
    )
    {
        var deadLetters = _managementClient.ReadDeadLettersAsync(
            PostgresQueueName.Create(path),
            count,
            ct
        );

        return await deadLetters.ToQueueMessages(_messageSerializer).ConfigureAwait(false);
    }

    public async Task<int> MoveDeadLetters(string path, int count, CancellationToken ct)
    {
        var result = await _managementClient.RequeueDeadLettersAsync(
            PostgresQueueName.Create(path),
            count,
            ct
        );
        return (int)result;
    }

    public async Task SendMessage(string path, string jsonBody, CancellationToken cancellationToken)
    {
        await _managementClient.SendMessage(
            PostgresQueueName.Create(path),
            jsonBody,
            cancellationToken
        );
    }

    public async Task SendMessages(
        string path,
        IEnumerable<string> jsonBodies,
        CancellationToken cancellationToken
    )
    {
        await _managementClient.SendMessages(
            PostgresQueueName.Create(path),
            jsonBodies,
            cancellationToken
        );
    }

    public Task CancelScheduledMessage(
        string path,
        long sequenceNumber,
        CancellationToken cancellationToken
    )
    {
        throw new NotSupportedException();
    }

    public QueueType QueueType => QueueType.Queue;
}
