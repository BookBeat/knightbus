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
with the [management API](../features/management.md):

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
