# Messages and mappings

Everything KnightBus processes is a message, and every message implements `IMessage`:

```csharp
public interface IMessage { }
```

You never implement `IMessage` directly. You implement one of the transport-specific interfaces that
derive from it, and that choice is what binds the message to a transport.

## Commands, events and requests

KnightBus distinguishes three kinds of message.

| Kind | Base | Semantics |
| --- | --- | --- |
| **Command** | `ICommand` | One logical recipient. Tells the receiver to do something. Lands on a queue. |
| **Event** | `IEvent` | One publisher, many receivers. States that something happened. Fans out to every subscription. |
| **Request** | `IRequest` | Request/response — the sender waits for a reply. Only implemented by the NATS transport. |

Each message has a 1:1 relationship with a queue or topic.

## Transport marker interfaces

Pick the interface for the transport you want to carry the message. This is the only thing that
decides routing.

| Transport | Command | Event | Request |
| --- | --- | --- | --- |
| Azure Service Bus | `IServiceBusCommand` | `IServiceBusEvent` | — |
| Azure Storage Queues | `IStorageQueueCommand` | — | — |
| PostgreSQL | `IPostgresCommand` | `IPostgresEvent` | — |
| Redis | `IRedisCommand` | `IRedisEvent` | — |
| NATS | `INatsCommand` | `INatsEvent` | `INatsRequest` |

```csharp
// Carried by Azure Service Bus
public class OrderPlaced : IServiceBusEvent
{
    public string OrderId { get; set; }
}

// Carried by Redis, in the very same application
public class ThumbnailRequested : IRedisCommand
{
    public string ImageId { get; set; }
}
```

Messages are plain classes. KnightBus does not require a base class, attributes or a parameterless
constructor beyond what your serializer needs.

## Message mappings

Every message needs an `IMessageMapping<T>` naming the queue or topic it uses.

```csharp
public class OrderPlacedMapping : IMessageMapping<OrderPlaced>
{
    public string QueueName => "order-placed";
}
```

!!! warning "The mapping must live in the same assembly as the message"
    Mappings are discovered by scanning the assembly that declares the message type. A mapping in
    another assembly is never found. When no mapping is found you get a
    `MessageMappingMissingException` with `No queue name mapping exists for {type}`.

Queue naming rules belong to the transport. PostgreSQL in particular only accepts letters, digits
and underscores — see [PostgreSQL](../transports/postgresql.md).

## Event subscriptions

An event needs one `IEventSubscription<T>` per independent listener. The subscription is what gives
each listener its own cursor over the stream, so a slow listener cannot lose messages for the
others.

```csharp
public class OrderPlaced : IServiceBusEvent
{
    public string OrderId { get; set; }
}

public class InvoicingSubscription : IEventSubscription<OrderPlaced>
{
    public string Name => "invoicing";
}

public class ShippingSubscription : IEventSubscription<OrderPlaced>
{
    public string Name => "shipping";
}
```

Each processor then names the subscription it listens on:

```csharp
public class InvoicingProcessor
    : IProcessEvent<OrderPlaced, InvoicingSubscription, DefaultSettings>
{
    public Task ProcessAsync(OrderPlaced message, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
```

!!! note
    Subscription types are instantiated by reflection, so they need a public parameterless
    constructor. Renaming the `Name` of a live subscription creates a *new* subscription that starts
    empty; the old one keeps accumulating messages until you remove it.

## Sending messages

Each transport exposes its own client interface, registered as **scoped**. Resolve it from a scope
or inject it into a scoped service.

```csharp
using var scope = host.Services.CreateScope();
var bus = scope.ServiceProvider.GetRequiredService<IServiceBus>();

await bus.SendAsync(new SampleCommand { Message = "Hello" });          // command
await bus.PublishEventAsync(new OrderPlaced { OrderId = "1" });         // event
```

The clients are deliberately thin wrappers over each transport, so their surfaces differ:

| Transport | Client | Send | Publish | Deferred delivery |
| --- | --- | --- | --- | --- |
| Azure Service Bus | `IServiceBus` | `SendAsync<T>(T)`, `SendAsync<T>(IEnumerable<T>)` | `PublishEventAsync<T>`, `PublishEventsAsync<T>` | `ScheduleAsync` → sequence number, `CancelScheduledAsync` |
| Azure Storage Queues | `IStorageBus` | `SendAsync<T>(T)` | — | `ScheduleAsync<T>(T, TimeSpan)` |
| PostgreSQL | `IPostgresBus` | `SendAsync<T>(T, ct)`, batch overload | `PublishAsync<T>(T, ct)` | `ScheduleAsync<T>(T, TimeSpan, ct)` |
| Redis | `IRedisBus` | `SendAsync<T>(T)`, batch overload | `PublishAsync<T>(T)` | — |
| NATS | `INatsBus` | `Send(INatsCommand)` | `Publish(INatsEvent)` | — |

Two inconsistencies to be aware of: Service Bus publishes with **`PublishEventAsync`** rather than
`PublishAsync`, and the NATS client's methods are **`Send`/`Publish`** without the `Async` suffix.
`IPostgresBus` requires an explicit `CancellationToken` on every call, while `IRedisBus` accepts none
at all.

## Pre-processing messages on send

An `IMessagePreProcessor` runs on the sending side and returns properties that ride along with the
message. This is how attachments and distributed tracing attach their metadata.

```csharp
public class TenantPreProcessor : IMessagePreProcessor
{
    private readonly ITenantContext _tenant;

    public TenantPreProcessor(ITenantContext tenant) => _tenant = tenant;

    public Task<IDictionary<string, object>> PreProcess<T>(
        T message,
        CancellationToken cancellationToken
    )
        where T : IMessage
    {
        IDictionary<string, object> properties = new Dictionary<string, object>
        {
            ["tenant"] = _tenant.Id,
        };
        return Task.FromResult(properties);
    }
}
```

Register it alongside the transport:

```csharp
services.AddScoped<IMessagePreProcessor, TenantPreProcessor>();
```

Every registered pre-processor runs for every outgoing message, and the resulting properties are
merged onto the transport message (Service Bus application properties, NATS headers, the PostgreSQL
`properties` column, and so on).

## See also

- [Message processors](processors.md) — handling the messages you just defined.
- [Serialization](serialization.md) — what goes on the wire, and how to change it.
- [Attachments](../features/attachments.md) — sending payloads too large for the transport.
- [Marker interfaces](../reference/marker-interfaces.md) — the complete list.
