# Azure Service Bus

The most fully-featured KnightBus transport: queues, topics with subscriptions, native
dead-lettering, and deferred messages that can be cancelled.

```bash
dotnet add package KnightBus.Azure.ServiceBus
dotnet add package KnightBus.Azure.ServiceBus.Messages
```

## Registration

=== "Connection string"

    ```csharp
    services
        .UseServiceBus(config => config.ConnectionString = connectionString)
        .RegisterProcessors()
        .UseTransport<ServiceBusTransport>();
    ```

=== "Managed identity"

    ```csharp
    services
        .UseServiceBus(config =>
        {
            config.FullyQualifiedNamespace = "yournamespace.servicebus.windows.net";
            config.Credential = new ManagedIdentityCredential();
        })
        .RegisterProcessors()
        .UseTransport<ServiceBusTransport>();
    ```

    Leave `ConnectionString` unset when using a credential. Supplying neither a connection string nor
    a namespace-plus-credential throws at startup.

## Messages

| Interface | Kind |
| --- | --- |
| `IServiceBusCommand` | Command — one queue, one consumer |
| `IServiceBusEvent` | Event — one topic, many subscriptions |

```csharp
public class OrderPlaced : IServiceBusEvent
{
    public string OrderId { get; set; }
}

public class OrderPlacedMapping : IMessageMapping<OrderPlaced>
{
    public string QueueName => "order-placed";
}
```

Queues, topics and subscriptions are created automatically the first time they are needed.

## Client

```csharp
using var scope = host.Services.CreateScope();
var bus = scope.ServiceProvider.GetRequiredService<IServiceBus>();
```

| Method | Notes |
| --- | --- |
| `SendAsync<T>(T message)` | Send one command. |
| `SendAsync<T>(IEnumerable<T> messages)` | Batched send. |
| `PublishEventAsync<T>(T message)` | Publish one event. |
| `PublishEventsAsync<T>(IEnumerable<T> messages)` | Batched publish. |
| `ScheduleAsync<T>(T message, TimeSpan span)` | Deferred send. Returns a sequence number. |
| `ScheduleAsync<T>(IEnumerable<T> messages, TimeSpan span)` | Batched deferred send. Returns one sequence number per message, in input order. |
| `CancelScheduledAsync<T>(long sequenceNumber)` | Cancel a deferred message. |

!!! note "It is `PublishEventAsync`, not `PublishAsync`"
    Service Bus is the one transport whose publish methods are named after events. Every method takes
    an optional `CancellationToken`.

## Deferred messages

The delay is relative, and the returned sequence number is the only handle for cancelling:

```csharp
var sequenceNumber = await bus.ScheduleAsync(
    new SendReminder { UserId = userId },
    TimeSpan.FromHours(2)
);

await bus.CancelScheduledAsync<SendReminder>(sequenceNumber);
```

Batched scheduling is atomic — either all messages are scheduled or none are. Scheduling is available
for commands only; there is no deferred publish for events.

## Entity creation options

To control how KnightBus creates the queue or topic, implement `IServiceBusCreationOptions` on the
message's **mapping**:

```csharp
public class OrderPlacedMapping : IMessageMapping<OrderPlaced>, IServiceBusCreationOptions
{
    public string QueueName => "order-placed";

    public bool EnablePartitioning => true;
    public bool SupportOrdering => false;
    public bool EnableBatchedOperations => true;
}
```

| Option | Default | Effect |
| --- | --- | --- |
| `EnablePartitioning` | `false` | Creates the entity as partitioned, for higher throughput. |
| `SupportOrdering` | `false` | Messages are forwarded to subscriptions in order. |
| `EnableBatchedOperations` | `true` | Server-side batching. |

Two caveats:

- **It is all or nothing.** A mapping that implements the interface supplies all three values; there
  is no per-property merge with the defaults.
- **It only applies at creation.** Changing these values does not reconfigure an entity that already
  exists — Azure requires most of them to be set when the entity is created.

To change the defaults for every message instead, adjust `DefaultCreationOptions` on the
configuration:

```csharp
services.UseServiceBus(config =>
{
    config.ConnectionString = connectionString;
    config.DefaultCreationOptions.EnablePartitioning = true;
});
```

The property itself is read-only on `IServiceBusConfiguration`, so mutate the existing options object
rather than assigning a new one. To assign one, build a concrete `ServiceBusConfiguration` and pass it
to the other overload:

```csharp
services.UseServiceBus(new ServiceBusConfiguration
{
    ConnectionString = connectionString,
    DefaultCreationOptions = new ServiceBusCreationOptions { EnablePartitioning = true },
});
```

## Forwarding subscriptions to queues

By default a subscriber reads its subscription directly, so its dead letters sit in the
subscription's dead letter queue. Requeueing them sends them back to the **topic**, and every other
subscription on that topic receives them again.

With forwarding, each subscription forwards its messages to a queue of its own, using Service Bus
auto-forwarding, and the subscriber reads that queue instead. Dead letters then land on the
subscriber's queue, and requeueing them reaches only that subscriber.

Forwarding is **off by default**. Turn it on for every subscription the host processes:

```csharp
services.UseServiceBus(config =>
{
    config.ConnectionString = connectionString;
    config.SubscriptionForwarding.Enabled = true;
});
```

Or choose per subscription, which overrides the host setting in either direction:

```csharp
public class OrderPlacedEmailSubscription
    : IEventSubscription<OrderPlaced>,
        IServiceBusForwardingSubscription
{
    public string Name => "email-order-placed";
    public bool ForwardToQueue => true;
}
```

The queue is named `{topic}.{subscription}` unless you set
`config.SubscriptionForwarding.QueueName`.

When a forwarded subscription starts, KnightBus creates the queue if it is missing and points the
subscription at it, creating the topic and subscription if needed. This also applies to an existing
subscription, so the host needs **Manage** rights on the namespace, as it already does for creating
entities. The queue is created with the same partitioning and batching options as the topic.

### Turning it on for an existing subscription

Enabling forwarding changes where the subscriber reads from, so treat it as a breaking change for
that subscriber:

- **Rolling deploys.** As soon as the first new instance starts, the subscription forwards to the
  queue. Old instances still reading the subscription get nothing more until they are replaced.
  Nothing is lost: the messages wait on the queue.
- **Rolling back.** A host without forwarding reads the subscription, which still forwards to the
  queue, so it receives nothing. Turning the setting off does **not** undo the forwarding. To go
  back, clear the subscription's *Forward to* in Azure, then drain the queue, for example by
  re-sending its messages to the subscription's topic.
- **Old dead letters.** Dead letters from before the switch stay on the subscription. The
  management API's `MoveDeadLetters` sends them to the forwarding queue, not to the topic, so they
  still reach only this subscriber.
- **Other tooling.** Anything that inspects the subscription for active messages or dead letters,
  such as health checks or unused-entity cleanup, has to look at the queue as well.
  `SubscriptionQueueProperties.ForwardTo` names the queue.

## Processing settings

Settings map onto Service Bus' own processor options. Messages are received in peek-lock mode and
completed by KnightBus, and `MessageLockTimeout` becomes the maximum auto lock-renewal duration —
which means Service Bus renews the lock for you up to that limit. This is why
`ExtendMessageLockDurationMiddleware` is neither needed nor supported here.

`DeadLetterDeliveryLimit` must be **lower** than the queue's own `MaxDeliveryCount`, or Service Bus
dead-letters the message before KnightBus does and the
[`IProcessBeforeDeadLetter<T>`](../features/error-handling.md#hooking-into-dead-lettering) hook never
runs.

## Dead letters

Service Bus provides a dead letter sub-queue per queue and per subscription. Read and requeue them
with the [management API](../features/management.md). Requeueing a subscription's dead letters sends
them to the topic, and so to every subscription, unless the subscription
[forwards to a queue](#forwarding-subscriptions-to-queues):

```csharp
services.AddServiceBusManagement(connectionString);
```

This is also the only transport supporting `PeekScheduled` (inspecting deferred messages before
delivery) and `CancelScheduledMessage` through the management API.

## Attachments

Service Bus messages are size-limited, so large payloads belong in
[attachments](../features/attachments.md). This package ships no attachment provider, which is not a
gap — the provider is a host-wide choice, and either of the two that exist can back attachments here.
Blob Storage is the usual pick:

```csharp
services
    .UseServiceBus(config => config.ConnectionString = connectionString)
    .UseBlobStorage(storageConnectionString)
    .UseBlobStorageAttachments()
    .UseTransport<ServiceBusTransport>();
```

Note that only `UseTransport<ServiceBusTransport>()` appears — `UseBlobStorage(...)` supplies the
storage account for the attachments and does not start any Storage Queues listener. The same pattern
gives Service Bus messages [sagas](../features/sagas.md) and
[singleton processing](../features/singleton-processing.md), which likewise ship in other packages.

## Serialization

Defaults to `NewtonsoftSerializer`. See [serialization](../concepts/serialization.md) to change it.

## Example

[`KnightBus.Samples.Azure.ServiceBus`](https://github.com/BookBeat/knightbus/tree/master/samples/KnightBus.Samples.Azure.ServiceBus)
demonstrates commands, events with two subscriptions, the dead letter hook, creation options,
management and OpenTelemetry. A separate producer example and an Aspire host that runs both against
the Service Bus emulator sit alongside it.
