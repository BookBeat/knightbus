using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace KnightBus.UI.Console;

public interface IQueueManager
{
    IEnumerable<QueueProperties> List(CancellationToken ct);
    Task<QueueProperties> Get(string path, CancellationToken ct);
    Task Delete(string path, CancellationToken ct);
    Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekDeadLetter(string name, int count, CancellationToken ct);
    Task<int> MoveDeadLetters(string name, int count, CancellationToken ct);
}

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
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekDeadLetter(string name, int count, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(_topic, name, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        return receiver.PeekMessagesAsync(count, cancellationToken: ct);
    }

    public Task<int> MoveDeadLetters(string name, int count, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

public class ServiceBusTopicManager : IQueueManager
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;

    public ServiceBusTopicManager(string connectionString)
    {
        _adminClient = new ServiceBusAdministrationClient(connectionString);
        _client = new ServiceBusClient(connectionString);
    }
    public IEnumerable<QueueProperties> List(CancellationToken ct)
    {
        var queues = _adminClient.GetTopicsRuntimePropertiesAsync((ct)).ToBlockingEnumerable(ct);
        foreach (var q in queues)
        {
            yield return q.ToQueueProperties(new ServiceBusSubscriptionManager(q.Name, _client, _adminClient));
        }
    }

    private IEnumerable<QueueProperties> ListSubscriptions(string topic, CancellationToken ct)
    {
        var queues = _adminClient.GetSubscriptionsRuntimePropertiesAsync(topic, ct).ToBlockingEnumerable(ct);
        foreach (var q in queues)
        {
            yield return q.ToQueueProperties(this);
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
public class ServiceBusQueueManager : IQueueManager
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;

    public ServiceBusQueueManager(string connectionString)
    {
        _adminClient = new ServiceBusAdministrationClient(connectionString);
        _client = new ServiceBusClient(connectionString);

    }

    public IEnumerable<QueueProperties> List(CancellationToken ct)
    {
        var queues = _adminClient.GetQueuesRuntimePropertiesAsync((ct)).ToBlockingEnumerable(ct);
        foreach (var q in queues)
        {
            yield return q.ToQueueProperties(this);
        }
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var props = await _adminClient.GetQueueRuntimePropertiesAsync(path, ct).ConfigureAwait(false);
        return props.Value.ToQueueProperties(this);
    }

    public Task Delete(string path, CancellationToken ct)
    {
        return _adminClient.DeleteQueueAsync(path, ct);
    }

    public Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekDeadLetter(string name, int count, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(name, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        return receiver.PeekMessagesAsync(count, cancellationToken: ct);
    }

    public Task<int> MoveDeadLetters(string name, int count, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(name, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var sender = _client.CreateSender(name);

        return MoveMessages(sender, receiver, count, 10);
    }

    private async Task<int> MoveMessages(ServiceBusSender sender, ServiceBusReceiver receiver, int receiveLimit, int batchSize)
    {
        batchSize = receiveLimit < batchSize ? receiveLimit : batchSize;
        // Receive messages
        var movedMessages = 0;
        while (movedMessages < receiveLimit)
        {
            // Receive batch
            var messages = await receiver.ReceiveMessagesAsync(batchSize).ConfigureAwait(false);
            if (messages.Count == 0)
            {
                // No more messages to move => We're done
                break;
            }

            // Resend clones of messages
            var clones = messages.Select(m => new ServiceBusMessage(m)).ToList();

            if (clones.All(c => c.PartitionKey == clones.First().PartitionKey))
            {
                await sender.SendMessagesAsync(clones).ConfigureAwait(false);
            }
            else
            {
                await Task.WhenAll(clones.Select(m => sender.SendMessageAsync(m))).ConfigureAwait(false);
            }

            // Complete original messages
            await Task.WhenAll(messages.Select(m => receiver.CompleteMessageAsync(m))).ConfigureAwait(false);

            // Keep track of received messages
            movedMessages += messages.Count;
        }

        // Return result
        return movedMessages;
    }
}
