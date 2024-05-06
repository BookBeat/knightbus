using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using KnightBus.Core.Management;
using QueueProperties = KnightBus.Core.Management.QueueProperties;

namespace KnightBus.Azure.ServiceBus.Management;

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
    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var subs = _adminClient.GetSubscriptionsRuntimePropertiesAsync(_topic, ct);
        var properties = new List<QueueProperties>();
        await foreach (var sub in subs)
            properties.Add(sub.ToQueueProperties(this, _topic));

        return properties;
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var sub = await _adminClient.GetSubscriptionRuntimePropertiesAsync(_topic, path, ct).ConfigureAwait(false);
        return sub.Value.ToQueueProperties(this, _topic);
    }

    public Task Delete(string path, CancellationToken ct)
    {
        return _adminClient.DeleteSubscriptionAsync(_topic, path, ct);
    }

    public async Task<IReadOnlyList<QueueMessage>> Peek(string name, int count, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(_topic, name);
        var messages = await receiver.PeekMessagesAsync(count, cancellationToken: ct).ConfigureAwait(false);
        return messages.Select(m =>
        {
            m.ApplicationProperties.TryGetValue("Exception", out var error);
            return new QueueMessage(
                Encoding.UTF8.GetString(m.Body),
                error?.ToString() ?? string.Empty,
                m.EnqueuedTime,
                m.ScheduledEnqueueTime != default ? m.ScheduledEnqueueTime : null,
                m.DeliveryCount,
                m.MessageId,
                m.ApplicationProperties);
        }).ToList();
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string path, int count, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(_topic, path, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var messages = await receiver.PeekMessagesAsync(count, cancellationToken: ct).ConfigureAwait(false);
        return messages.Select(m =>
        {
            m.ApplicationProperties.TryGetValue("Exception", out var error);
            return new QueueMessage(
                Encoding.UTF8.GetString(m.Body),
                error?.ToString() ?? string.Empty,
                m.EnqueuedTime,
                m.ScheduledEnqueueTime != default ? m.ScheduledEnqueueTime : null,
                m.DeliveryCount,
                m.MessageId,
                m.ApplicationProperties);
        }).ToList();
    }

    public async Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(string path, int receiveLimit, CancellationToken ct)
    {
        var queueMessages = new List<QueueMessage>();
        var receiver = _client.CreateReceiver(_topic, path, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        // Receive messages
        var movedMessages = 0;
        var batchSize = 10;
        while (movedMessages < receiveLimit)
        {
            batchSize = batchSize > (receiveLimit - movedMessages) ? receiveLimit - movedMessages : batchSize;
            // Receive batch
            var messages = await receiver.ReceiveMessagesAsync(batchSize, cancellationToken: ct).ConfigureAwait(false);
            queueMessages.AddRange(
                messages.Select(
                    m =>
                    {
                        m.ApplicationProperties.TryGetValue("Exception", out var error);
                        return new QueueMessage(
                            Encoding.UTF8.GetString(m.Body),
                            error?.ToString() ?? string.Empty,
                            m.EnqueuedTime,
                            m.ScheduledEnqueueTime != default ? m.ScheduledEnqueueTime : null,
                            m.DeliveryCount,
                            m.MessageId, m.ApplicationProperties);
                    }));
            if (messages.Count == 0)
            {
                // No more messages to move => We're done
                break;
            }

            // Complete original messages
            await Task.WhenAll(messages.Select(m => receiver.CompleteMessageAsync(m, ct))).ConfigureAwait(false);

            // Keep track of received messages
            movedMessages += messages.Count;
        }

        return queueMessages;
    }

    public Task<int> MoveDeadLetters(string path, int count, CancellationToken ct)
    {

        var receiver = _client.CreateReceiver(_topic, path, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var sender = _client.CreateSender(_topic);

        return ServiceBusQueueManager.MoveMessages(sender, receiver, count, 10);
    }
    public QueueType QueueType => QueueType.Subscription;
}
