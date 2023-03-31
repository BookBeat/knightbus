using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace KnightBus.UI.Console.Providers.ServiceBus;

public class ServiceBusSubscriptionManager : IQueueManager
{
    private readonly string _topic;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;

    public ServiceBusSubscriptionManager(string topic, ServiceBusClient client, ServiceBusAdministrationClient adminClient)
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

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string name, int count, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(_topic, name, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var messages = await receiver.PeekMessagesAsync(count, cancellationToken: ct).ConfigureAwait(false);
        return messages.Select(m =>
        {
            m.ApplicationProperties.TryGetValue("Exception", out var error);
            return new QueueMessage(Encoding.UTF8.GetString(m.Body), error ?? string.Empty, m.EnqueuedTime);
        }).ToList();
    }

    public Task<int> MoveDeadLetters(string name, int count, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(_topic, name, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var sender = _client.CreateSender(_topic);

        return ServiceBusQueueManager.MoveMessages(sender, receiver, count, 10);
    }
}
