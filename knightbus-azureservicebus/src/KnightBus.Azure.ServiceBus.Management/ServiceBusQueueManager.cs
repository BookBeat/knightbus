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

public class ServiceBusQueueManager : IQueueManager, IQueueMessageSender
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;

    public ServiceBusQueueManager(IServiceBusConfiguration configuration)
    {
        _adminClient = new ServiceBusAdministrationClient(configuration.ConnectionString);
        _client = new ServiceBusClient(configuration.ConnectionString);
    }

    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var queues = _adminClient.GetQueuesRuntimePropertiesAsync(ct);
        var properties = new List<QueueProperties>();
        await foreach (var q in queues)
            properties.Add(q.ToQueueProperties(this));

        return properties;
    }

    public async Task<QueueProperties> Get(string path, CancellationToken ct)
    {
        var props = await _adminClient
            .GetQueueRuntimePropertiesAsync(path, ct)
            .ConfigureAwait(false);
        return props.Value.ToQueueProperties(this);
    }

    public Task Delete(string path, CancellationToken ct)
    {
        return _adminClient.DeleteQueueAsync(path, ct);
    }

    public async Task<IReadOnlyList<QueueMessage>> Peek(
        string name,
        int count,
        CancellationToken ct
    )
    {
        var receiver = _client.CreateReceiver(name);
        var messages = await receiver
            .PeekMessagesAsync(count, cancellationToken: ct)
            .ConfigureAwait(false);
        return messages
            .Select(m =>
            {
                m.ApplicationProperties.TryGetValue("Exception", out var error);
                return new QueueMessage(
                    Encoding.UTF8.GetString(m.Body),
                    error?.ToString() ?? string.Empty,
                    m.EnqueuedTime,
                    m.ScheduledEnqueueTime != default ? m.ScheduledEnqueueTime : null,
                    m.DeliveryCount,
                    m.MessageId,
                    m.ApplicationProperties
                );
            })
            .ToList();
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(
        string path,
        int count,
        CancellationToken ct
    )
    {
        var receiver = _client.CreateReceiver(
            path,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter }
        );
        var messages = await receiver
            .PeekMessagesAsync(count, cancellationToken: ct)
            .ConfigureAwait(false);
        var queueMessages = messages
            .Select(m =>
            {
                m.ApplicationProperties.TryGetValue("Exception", out var error);
                return new QueueMessage(
                    Encoding.UTF8.GetString(m.Body),
                    error?.ToString() ?? string.Empty,
                    m.EnqueuedTime,
                    m.ScheduledEnqueueTime != default ? m.ScheduledEnqueueTime : null,
                    m.DeliveryCount,
                    m.MessageId,
                    m.ApplicationProperties
                );
            })
            .ToList();

        return queueMessages;
    }

    public async Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(
        string path,
        int receiveLimit,
        CancellationToken ct
    )
    {
        var queueMessages = new List<QueueMessage>();
        var receiver = _client.CreateReceiver(
            path,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter }
        );

        // Receive messages
        var movedMessages = 0;
        var batchSize = 10;
        while (movedMessages < receiveLimit)
        {
            batchSize =
                batchSize > (receiveLimit - movedMessages)
                    ? receiveLimit - movedMessages
                    : batchSize;
            // Receive batch
            var messages = await receiver
                .ReceiveMessagesAsync(batchSize, cancellationToken: ct)
                .ConfigureAwait(false);
            queueMessages.AddRange(
                messages.Select(m =>
                {
                    m.ApplicationProperties.TryGetValue("Exception", out var error);
                    return new QueueMessage(
                        Encoding.UTF8.GetString(m.Body),
                        error?.ToString() ?? string.Empty,
                        m.EnqueuedTime,
                        m.ScheduledEnqueueTime != default ? m.ScheduledEnqueueTime : null,
                        m.DeliveryCount,
                        m.MessageId,
                        m.ApplicationProperties
                    );
                })
            );
            if (messages.Count == 0)
            {
                // No more messages to move => We're done
                break;
            }

            // Complete original messages
            await Task.WhenAll(messages.Select(m => receiver.CompleteMessageAsync(m, ct)))
                .ConfigureAwait(false);

            // Keep track of received messages
            movedMessages += messages.Count;
        }

        return queueMessages;
    }

    public Task<int> MoveDeadLetters(string path, int count, CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(
            path,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter }
        );
        var sender = _client.CreateSender(path);

        return MoveMessages(sender, receiver, count, 10);
    }

    public QueueType QueueType => QueueType.Queue;

    public async Task SendMessage(string path, string jsonBody, CancellationToken cancellationToken)
    {
        var sender = _client.CreateSender(path);
        await sender.SendMessageAsync(
            new ServiceBusMessage(jsonBody) { ContentType = "application/json" },
            cancellationToken
        );
    }

    public async Task SendMessages(
        string path,
        IEnumerable<string> jsonBodies,
        CancellationToken cancellationToken
    )
    {
        var sender = _client.CreateSender(path);
        await sender.SendMessagesAsync(
            jsonBodies.Select(x => new ServiceBusMessage(x) { ContentType = "application/json" }),
            cancellationToken
        );
    }

    internal static async Task<int> MoveMessages(
        ServiceBusSender sender,
        ServiceBusReceiver receiver,
        int receiveLimit,
        int batchSize
    )
    {
        // Receive messages
        var movedMessages = 0;
        while (movedMessages < receiveLimit)
        {
            batchSize =
                batchSize > (receiveLimit - movedMessages)
                    ? receiveLimit - movedMessages
                    : batchSize;
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
                await Task.WhenAll(clones.Select(m => sender.SendMessageAsync(m)))
                    .ConfigureAwait(false);
            }

            // Complete original messages
            await Task.WhenAll(messages.Select(m => receiver.CompleteMessageAsync(m)))
                .ConfigureAwait(false);

            // Keep track of received messages
            movedMessages += messages.Count;
        }

        // Return result
        return movedMessages;
    }
}
