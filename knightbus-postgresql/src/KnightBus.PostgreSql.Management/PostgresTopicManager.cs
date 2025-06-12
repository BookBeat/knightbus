using KnightBus.Core.Management;

namespace KnightBus.PostgreSql.Management;

public class PostgresTopicManager : IQueueManager, IQueueMessageSender
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
        return topics.Select(t => new QueueProperties(
            t.Name,
            new PostgresSubscriptionManager(t.Name, _managementClient, _configuration),
            false,
            QueueType.Topic
        ));
    }

    public Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var topic = new QueueProperties(
            path,
            new PostgresSubscriptionManager(path, _managementClient, _configuration),
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

    public async Task SendMessage(string path, string jsonBody, CancellationToken cancellationToken)
    {
        await _managementClient.PublishEvent(path, jsonBody, cancellationToken);
    }

    public QueueType QueueType => QueueType.Topic;
}
