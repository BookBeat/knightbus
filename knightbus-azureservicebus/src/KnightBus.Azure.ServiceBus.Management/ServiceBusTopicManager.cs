using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core.Management;
using QueueProperties = KnightBus.Core.Management.QueueProperties;

namespace KnightBus.Azure.ServiceBus.Management;

public class ServiceBusTopicManager : IQueueManager, IAsyncDisposable
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;
    private bool _disposed;

    public ServiceBusTopicManager(IServiceBusConfiguration configuration)
    {
        _adminClient = ServiceBusClientFactory.CreateServiceBusAdministrationClient(configuration);
        _client = ServiceBusClientFactory.CreateServiceBusClient(configuration);
    }

    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var queues = _adminClient.GetTopicsRuntimePropertiesAsync((ct));
        var properties = new List<QueueProperties>();
        await foreach (var q in queues)
            properties.Add(
                q.ToQueueProperties(
                    new ServiceBusSubscriptionManager(q.Name, _client, _adminClient)
                )
            );

        return properties;
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var topic = await _adminClient
            .GetTopicRuntimePropertiesAsync(path, ct)
            .ConfigureAwait(false);
        return topic.Value.ToQueueProperties(
            new ServiceBusSubscriptionManager(path, _client, _adminClient)
        );
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

    public QueueType QueueType => QueueType.Topic;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_client != null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        _disposed = true;
    }
}
