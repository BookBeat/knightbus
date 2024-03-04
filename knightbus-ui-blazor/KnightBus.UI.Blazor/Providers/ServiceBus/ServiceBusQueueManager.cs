﻿using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace KnightBus.UI.Blazor.Providers.ServiceBus;

public class ServiceBusQueueManager : IQueueManager
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusClient _client;

    public ServiceBusQueueManager(EnvironmentService environmentService, IServiceProvider provider)
    {
        var configuration = provider.GetRequiredKeyedService<EnvironmentConfig>(environmentService.Get());
        _adminClient = new ServiceBusAdministrationClient(configuration.ServiceBusConnectionString);
        _client = new ServiceBusClient(configuration.ServiceBusConnectionString);
    }

    public async Task<IEnumerable<QueueProperties>> List(CancellationToken ct)
    {
        var queues = _adminClient.GetQueuesRuntimePropertiesAsync((ct));
        var properties = new List<QueueProperties>();
        await foreach (var q in queues)
            properties.Add(q.ToQueueProperties(this));

        return properties;
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

    public async Task<IReadOnlyList<QueueMessage>> Peek(
        string name,
        int count,
        CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(name);
        var messages = await receiver.PeekMessagesAsync(count, cancellationToken: ct).ConfigureAwait(false);
        return messages.Select(
            m =>
            {
                m.ApplicationProperties.TryGetValue("Exception", out var error);
                return new QueueMessage(
                    Encoding.UTF8.GetString(m.Body),
                    error ?? string.Empty,
                    m.EnqueuedTime,
                    m.DeliveryCount,
                    m.MessageId);
            }).ToList();
    }

    public async Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(
        string name,
        int count,
        CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(name, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var messages = await receiver.PeekMessagesAsync(count, cancellationToken: ct).ConfigureAwait(false);
        var queueMessages = messages.Select(
            m =>
            {
                m.ApplicationProperties.TryGetValue("Exception", out var error);
                return new QueueMessage(
                    Encoding.UTF8.GetString(m.Body),
                    error ?? string.Empty,
                    m.EnqueuedTime,
                    m.DeliveryCount,
                    m.MessageId);
            }).ToList();

        return queueMessages;
    }

    public async Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(
        string name,
        int receiveLimit,
        CancellationToken ct)
    {
        var queueMessages = new List<QueueMessage>();
        var receiver = _client.CreateReceiver(name, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

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
                            error ?? string.Empty,
                            m.EnqueuedTime,
                            m.DeliveryCount,
                            m.MessageId);
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

    public Task<int> MoveDeadLetters(
        string name,
        int count,
        CancellationToken ct)
    {
        var receiver = _client.CreateReceiver(name, new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var sender = _client.CreateSender(name);

        return MoveMessages(
            sender,
            receiver,
            count,
            10);
    }

    public string DisplayName => "ServiceBus Queue";
    public QueueType QueueType => QueueType.Queue;

    internal static async Task<int> MoveMessages(
        ServiceBusSender sender,
        ServiceBusReceiver receiver,
        int receiveLimit,
        int batchSize)
    {
        // Receive messages
        var movedMessages = 0;
        while (movedMessages < receiveLimit)
        {
            batchSize = batchSize > (receiveLimit - movedMessages) ? receiveLimit - movedMessages : batchSize;
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
