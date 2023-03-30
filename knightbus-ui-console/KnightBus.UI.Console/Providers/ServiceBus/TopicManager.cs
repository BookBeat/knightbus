using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace KnightBus.UI.Console.Providers.ServiceBus;

public class TopicManager : IQueueManager
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;

    public TopicManager(string connectionString)
    {
        _adminClient = new ServiceBusAdministrationClient(connectionString);
        _client = new ServiceBusClient(connectionString);
    }
    public IEnumerable<QueueProperties> List(CancellationToken ct)
    {
        var queues = _adminClient.GetTopicsRuntimePropertiesAsync((ct)).ToBlockingEnumerable(ct);
        foreach (var q in queues)
        {
            yield return q.ToQueueProperties(new SubscriptionManager(q.Name, _client, _adminClient));
        }
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var topic = await _adminClient.GetTopicRuntimePropertiesAsync(path, ct).ConfigureAwait(false);
        return topic.Value.ToQueueProperties(this);
    }

    public Task Delete(string path, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekDeadLetter(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<int> MoveDeadLetters(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
