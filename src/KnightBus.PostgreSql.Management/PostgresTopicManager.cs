using KnightBus.Core.Management;

namespace KnightBus.PostgreSql.Management;

public class PostgresTopicManager : IQueueManager
{
    private readonly PostgresManagementClient _managementClient;
    private readonly IPostgresConfiguration _configuration;

    public PostgresTopicManager(
        PostgresManagementClient managementClient,
        IPostgresConfiguration configuration
    )
    {
        _managementClient = managementClient;
        _configuration = configuration;
    }

    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var topics = await _managementClient.ListTopics(ct);
        var result = new List<QueueProperties>();
        foreach (var t in topics)
        {
            // Table names are read back from the database, but anything KnightBus could not
            // have created is skipped rather than interpolated into SQL further down.
            if (!PostgresQueueName.TryCreate(t.Name, out var topic))
                continue;

            result.Add(
                new QueueProperties(
                    topic.Value,
                    new PostgresSubscriptionManager(topic, _managementClient, _configuration),
                    false,
                    QueueType.Topic
                )
            );
        }

        return result;
    }

    public Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var topicName = PostgresQueueName.Create(path);
        var topic = new QueueProperties(
            topicName.Value,
            new PostgresSubscriptionManager(topicName, _managementClient, _configuration),
            false,
            QueueType.Topic
        );
        return Task.FromResult(topic);
    }

    public Task Delete(string path, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<QueueMessage>> Peek(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<QueueMessage>> PeekScheduled(
        string name,
        int count,
        CancellationToken ct
    )
    {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(
        string path,
        int count,
        CancellationToken ct
    )
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(
        string path,
        int count,
        CancellationToken ct
    )
    {
        throw new NotImplementedException();
    }

    public Task<int> MoveDeadLetters(string path, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public QueueType QueueType => QueueType.Topic;
}
