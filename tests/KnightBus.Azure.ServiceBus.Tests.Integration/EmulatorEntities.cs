using Azure.Messaging.ServiceBus;

namespace KnightBus.Azure.ServiceBus.Tests.Integration;

internal static class EmulatorEntities
{
    private static readonly TimeSpan EmptyWait = TimeSpan.FromMilliseconds(250);

    // The emulator keeps its state for the whole run, so fixtures empty their entities before each test
    public static async Task PurgeQueue(ServiceBusClient client, string queue)
    {
        await Drain(client.CreateReceiver(queue, ReceiveAndDelete()));
        await Drain(client.CreateReceiver(queue, ReceiveAndDelete(SubQueue.DeadLetter)));
        await CancelScheduled(client, queue);
    }

    public static async Task PurgeSubscription(
        ServiceBusClient client,
        string topic,
        string subscription
    )
    {
        await Drain(client.CreateReceiver(topic, subscription, ReceiveAndDelete()));
        await Drain(
            client.CreateReceiver(topic, subscription, ReceiveAndDelete(SubQueue.DeadLetter))
        );
    }

    public static async Task<IReadOnlyList<ServiceBusReceivedMessage>> Receive(
        ServiceBusReceiver receiver,
        int count,
        TimeSpan? maxWait = null
    )
    {
        var received = new List<ServiceBusReceivedMessage>();
        while (received.Count < count)
        {
            var batch = await receiver.ReceiveMessagesAsync(
                count - received.Count,
                maxWait ?? EmptyWait
            );
            if (batch.Count == 0)
                break;
            received.AddRange(batch);
        }
        return received;
    }

    // A message published to a topic reaches its subscriptions a moment later on the emulator
    public static async Task<T> Eventually<T>(Func<Task<T>> read, Func<T, bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        var value = await read();
        while (!done(value) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            value = await read();
        }
        return value;
    }

    // Dead-letters the message the way a failing KnightBus handler leaves it, with the error in "Exception"
    public static async Task SendToDeadLetter(
        ServiceBusSender sender,
        ServiceBusReceiver receiver,
        string body
    )
    {
        await sender.SendMessageAsync(new ServiceBusMessage(body));
        var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5));
        await receiver.DeadLetterMessageAsync(
            message,
            new Dictionary<string, object> { ["Exception"] = "boom" }
        );
    }

    private static ServiceBusReceiverOptions ReceiveAndDelete(SubQueue subQueue = SubQueue.None) =>
        new() { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete, SubQueue = subQueue };

    private static async Task Drain(ServiceBusReceiver receiver)
    {
        await using (receiver)
        {
            while ((await receiver.ReceiveMessagesAsync(100, EmptyWait)).Count > 0) { }
        }
    }

    private static async Task CancelScheduled(ServiceBusClient client, string queue)
    {
        await using var receiver = client.CreateReceiver(queue);
        await using var sender = client.CreateSender(queue);
        var peeked = await receiver.PeekMessagesAsync(100, fromSequenceNumber: 0);
        foreach (var message in peeked.Where(m => m.State == ServiceBusMessageState.Scheduled))
            await sender.CancelScheduledMessageAsync(message.SequenceNumber);
    }
}
