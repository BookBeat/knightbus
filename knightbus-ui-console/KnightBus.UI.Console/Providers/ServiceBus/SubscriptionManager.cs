using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace KnightBus.UI.Console.Providers.ServiceBus;

public class SubscriptionManager : IQueueManager
{
    private readonly string _topic;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;

    public SubscriptionManager(string topic, ServiceBusClient client, ServiceBusAdministrationClient adminClient)
    {
        _topic = topic;
        _adminClient = adminClient;
        _client = client;
    }
    public IEnumerable<QueueProperties> List(CancellationToken ct)
    {
        var subs = _adminClient.GetSubscriptionsRuntimePropertiesAsync(_topic, ct).ToBlockingEnumerable(ct);
        foreach (var sub in subs)
        {
            yield return sub.ToQueueProperties(this);
        }
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var sub = await _adminClient.GetSubscriptionRuntimePropertiesAsync(_topic, path, ct).ConfigureAwait(false);
        return sub.Value.ToQueueProperties(this);
    }

    public Task Delete(string path, CancellationToken ct)
    {
        return _adminClient.DeleteSubscriptionAsync(_topic, path, ct);
    }

    public Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekDeadLetter(string name, int count, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(_topic, name, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        return receiver.PeekMessagesAsync(count, cancellationToken: ct);
    }

    public Task<int> MoveDeadLetters(string name, int count, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(_topic, name, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var sender = _client.CreateSender(_topic);

        return QueueManager.MoveMessages(sender, receiver, count, 10);
    }
}
