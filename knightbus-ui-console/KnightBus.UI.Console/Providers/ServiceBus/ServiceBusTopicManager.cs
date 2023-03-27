using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Azure.ServiceBus;

namespace KnightBus.UI.Console.Providers.ServiceBus;

public class ServiceBusTopicManager : IQueueManager
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;

    public ServiceBusTopicManager(IServiceBusConfiguration configuration)
    {
        _adminClient = new ServiceBusAdministrationClient(configuration.ConnectionString);
        _client = new ServiceBusClient(configuration.ConnectionString);
    }
    public IEnumerable<QueueProperties> List(CancellationToken ct)
    {
        var queues = _adminClient.GetTopicsRuntimePropertiesAsync((ct)).ToBlockingEnumerable(ct);
        foreach (var q in queues)
        {
            yield return q.ToQueueProperties(new ServiceBusSubscriptionManager(q.Name, _client, _adminClient));
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

    public Task<IReadOnlyList<QueueMessage>> Peek(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<int> MoveDeadLetters(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public string DisplayName => "ServiceBus Topic";
    public QueueType QueueType => QueueType.Topic;
}
