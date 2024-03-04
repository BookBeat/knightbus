using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace KnightBus.UI.Blazor.Providers.ServiceBus;

public class ServiceBusTopicManager : IQueueManager
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;

    public ServiceBusTopicManager(EnvironmentService environmentService, IServiceProvider provider)
    {
        var configuration = provider.GetRequiredKeyedService<EnvironmentConfig>(environmentService.Get());
        _adminClient = new ServiceBusAdministrationClient(configuration.ServiceBusConnectionString);
        _client = new ServiceBusClient(configuration.ServiceBusConnectionString);
    }
    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var queues = _adminClient.GetTopicsRuntimePropertiesAsync((ct));
        var properties = new List<QueueProperties>();
        await foreach (var q in queues)
            properties.Add(q.ToQueueProperties(new ServiceBusSubscriptionManager(q.Name, _client, _adminClient)));

        return properties;
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

    public Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(string name, int count, CancellationToken ct)
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
