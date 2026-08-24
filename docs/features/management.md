# Management API

`KnightBus.Core.Management` gives you a transport-agnostic way to inspect and manipulate queues:
list them, read depths, peek messages, and read or requeue dead letters. It is what you build an
operations dashboard or a support tool on.

```bash
dotnet add package KnightBus.Core.Management
```

## Registration

Register the management package for each transport you want to manage. Each call also registers that
transport's configuration and client, so it works standalone in a tool that does not host any
listeners.

=== "Azure Service Bus"

    ```csharp
    services.AddServiceBusManagement(connectionString);
    ```

=== "Azure Storage Queues"

    ```csharp
    services.UseBlobStorageManagement(connectionString);
    ```

=== "PostgreSQL"

    ```csharp
    services.UsePostgresManagement(connectionString);
    ```

=== "Redis"

    ```csharp
    services.UseRedisManagement(connectionString, databaseId: 0);
    ```

Service Bus, Storage Queues and PostgreSQL each have a further overload taking a configuration
callback, which is how you reach managed identity and other options. `UseRedisManagement` has only the
form above. PostgreSQL additionally offers `UsePostgresManagementWithAzureManagedIdentity` in
`KnightBus.PostgreSql.Management.Extensions.Azure`.

## Resolving managers

A transport registers one `IQueueManager` per entity kind — queues, topics and subscriptions are
separate managers. **Resolve the collection, not a single instance:**

```csharp
public class QueueBrowser
{
    private readonly IEnumerable<IQueueManager> _managers;

    public QueueBrowser(IEnumerable<IQueueManager> managers) => _managers = managers;

    public async Task<List<QueueProperties>> ListAll(CancellationToken ct)
    {
        var all = new List<QueueProperties>();
        foreach (var manager in _managers)
        {
            all.AddRange(await manager.List(ct));
        }
        return all;
    }
}
```

!!! warning "Injecting a bare `IQueueManager` gives you only the last registration"
    Because several managers are registered against the same interface, resolving `IQueueManager`
    directly silently yields just one of them — usually not the one you wanted. Inject
    `IEnumerable<IQueueManager>`, or inject a concrete manager (`ServiceBusQueueManager`,
    `PostgresTopicManager`, `StorageQueueManager`, `RedisQueueManager`, …) when you know the transport.

Every `QueueProperties` carries the `IQueueManager` that produced it, so follow-up operations route
themselves correctly:

```csharp
var queue = (await manager.List(ct)).First(q => q.Name == "order-placed");
var messages = await queue.Manager.Peek(queue.Name, 10, ct);
```

Subscription managers are never registered in DI. Reach them through their topic manager's `Get`,
which returns `SubscriptionQueueProperties` with the right manager attached.

## Reading queues

```csharp
IEnumerable<QueueProperties> queues = await manager.List(ct);
QueueProperties queue = await manager.Get("order-placed", ct);
```

`QueueProperties` exposes the counts you would put on a dashboard: `TotalMessageCount`,
`ActiveMessageCount`, `DeadLetterMessageCount`, `ScheduledMessageCount`, `SizeInBytes`, plus
`CreatedAt`/`UpdatedAt`/`AccessedAt` and the `Type` (`Queue`, `Topic` or `Subscription`). For a topic,
`HasSubQueues` is true and `Get` enumerates its subscriptions.

## Peeking messages

```csharp
IReadOnlyList<QueueMessage> messages = await manager.Peek("order-placed", 10, ct);
```

Peeking is non-destructive. Each `QueueMessage` has the raw `Body`, an `Error` (populated for dead
letters), `Time`, `ScheduledTime`, `DeliveryCount`, `MessageId`, `Properties` and, on Service Bus, the
`SequenceNumber`.

`PeekScheduled` inspects deferred messages before their delivery time, and works only on the Service
Bus **queue** manager. Everywhere else it throws — `NotSupportedException` on the Storage Queues,
Redis and PostgreSQL managers, `NotImplementedException` on the Service Bus topic and subscription
managers.

## Dead letters

This is what the API is mostly used for:

```csharp
// Look, without consuming
var deadLetters = await manager.PeekDeadLetter("order-placed", 50, ct);

// Consume and remove
var drained = await manager.ReadDeadLetter("order-placed", 50, ct);

// Replay back onto the original queue
var moved = await manager.MoveDeadLetters("order-placed", 50, ct);
```

The usual recovery sequence is: peek to diagnose, deploy the fix, then `MoveDeadLetters` to replay.
See [errors and dead-lettering](error-handling.md).

Two transport-specific quirks are worth knowing before you trust the numbers:

- **Azure Storage Queues** — `MoveDeadLetters` returns the count you *requested*, not the count
  actually moved.
- **PostgreSQL** — dead letter rows carry no delivery count, so `DeliveryCount` always reads `0` for
  them. The failure reason is in the `error_message` property.

## Sending messages

`IQueueMessageSender` sends raw JSON to a queue, which is how you replay or hand-craft a message from
a tool:

```csharp
await sender.SendMessage("order-placed", """{"OrderId":"1"}""", ct);
await sender.SendMessages("order-placed", jsonBodies, ct);
await sender.CancelScheduledMessage("order-placed", sequenceNumber, ct);
```

Only **Service Bus and PostgreSQL** register an `IQueueMessageSender`; injecting it with only Storage
Queues or Redis registered fails. `CancelScheduledMessage` is Service Bus only and throws
`NotSupportedException` on PostgreSQL. As with `IQueueManager`, registering two transports makes a
single-instance resolution ambiguous — inject `IEnumerable<IQueueMessageSender>` or the concrete
manager.

## Attachments of dead-lettered messages

With Blob Storage management registered, `IQueueMessageAttachmentProvider` reads the attachment of a
message you have peeked — useful because
[attachments of dead-lettered messages are deliberately kept](attachments.md#lifecycle-and-cleanup).

```csharp
var properties = message.Properties.ToDictionary(p => p.Key, p => p.Value);

if (attachments.HasAttachment(properties))
{
    QueueMessageAttachment attachment =
        await attachments.GetAttachment(queueName, properties, ct);
}
```

`QueueMessage.Properties` is an `IReadOnlyDictionary<string, string>` while both methods take a
concrete `Dictionary<string, string>`, hence the copy.

## Support matrix

| Operation | Service Bus | Storage Queues | PostgreSQL | Redis |
| --- | :--: | :--: | :--: | :--: |
| `List` / `Get` | ✅ | ✅ | ✅ | ✅ |
| `Peek` | ✅ | ✅ | ✅ | ✅ |
| `PeekScheduled` | ✅ | — | — | — |
| `PeekDeadLetter` / `ReadDeadLetter` | ✅ | ✅ | ✅ | ✅ |
| `MoveDeadLetters` | ✅ | ✅ | ✅ | ✅ |
| `Delete` | ✅ | ✅ | ✅ | ✅ |
| `IQueueMessageSender` | ✅ | — | ✅ | — |
| `CancelScheduledMessage` | ✅ | — | — | — |
| Topics and subscriptions | ✅ | n/a | ✅ | n/a |

Topic managers are metadata-only: `List` and `Get` enumerate subscriptions, and the message
operations throw. Operate on the subscription managers instead. NATS has no management support at all.
