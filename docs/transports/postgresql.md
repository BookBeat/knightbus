# PostgreSQL

Commands, events and deferred delivery on top of a database you probably already run. No extra broker
to operate, and queue writes can participate in the same PostgreSQL instance as your application
data.

```bash
dotnet add package KnightBus.PostgreSql
dotnet add package KnightBus.PostgreSql.Messages
```

## Registration

=== "Connection string"

    ```csharp
    services
        .UsePostgres(config =>
        {
            config.ConnectionString = connectionString;
            config.PollingDelay = TimeSpan.FromMilliseconds(250);
        })
        .RegisterProcessors()
        .UseTransport<PostgresTransport>();
    ```

=== "Azure managed identity"

    Add `KnightBus.PostgreSql.Extensions.Azure`:

    ```csharp
    services
        .UsePostgresWithAzureManagedIdentity(config =>
        {
            config.ConnectionString = connectionString;
            config.TokenCredential = new ManagedIdentityCredential();
        })
        .RegisterProcessors()
        .UseTransport<PostgresTransport>();
    ```

    Tokens are refreshed every 55 minutes by default, retrying every 10 seconds on failure. Any
    password in the connection string is stripped, since Npgsql rejects having both.

## Messages

| Interface | Kind |
| --- | --- |
| `IPostgresCommand` | Command |
| `IPostgresEvent` | Event |

!!! warning "Queue names may only contain letters, digits and underscores"
    A name containing `-` — the usual KnightBus convention — throws `ArgumentException`. Use
    underscores instead:

    ```csharp
    public class OrderPlacedMapping : IMessageMapping<OrderPlaced>
    {
        public string QueueName => "order_placed";   // not "order-placed"
    }
    ```

    The exception message mentions only `-`, but the rule rejects every character that is not a
    letter, digit or underscore. It surfaces at host startup, when the listener for that queue
    starts. This is the most common first-run failure on this transport.

## Client

```csharp
var bus = scope.ServiceProvider.GetRequiredService<IPostgresBus>();

await bus.SendAsync(new OrderPlaced { OrderId = "1" }, cancellationToken);
await bus.PublishAsync(new OrderPlaced { OrderId = "2" }, cancellationToken);
await bus.ScheduleAsync(new SendReminder(), TimeSpan.FromHours(1), cancellationToken);
```

Batch overloads exist for send, publish and schedule. Every method **requires** an explicit
`CancellationToken` — this is the only client without defaults.

Batches of 50 or more messages are inserted with a binary `COPY`, smaller ones with a batched
`INSERT`, so sending in chunks of a few hundred is considerably faster than one at a time.

## Polling and latency

This is a polling transport. `PollingDelay` (default **5 seconds**) is how long a listener waits after
finding an empty queue, so it also sets your worst-case idle latency. Lower it for
latency-sensitive work:

```csharp
config.PollingDelay = TimeSpan.FromMilliseconds(250);
```

The trade-off is database load: every listener polls on this interval, so many queues with a short
delay means a steady query load even when idle.

## Database objects

Everything lives in the `knightbus` schema, created on demand along with the tables:

| Object | Purpose |
| --- | --- |
| `knightbus.q_{queue}` | Queue |
| `knightbus.dlq_{queue}` | Queue dead letters |
| `knightbus.t_{topic}` | Topic — holds the list of subscription names |
| `knightbus.s_{topic}_{subscription}` | One queue per subscription |
| `knightbus.dlq_{topic}_{subscription}` | Subscription dead letters |
| `knightbus.metadata` | Registry of created queues |
| `knightbus.sagas` | Saga state, if the saga store is enabled |
| `knightbus.publish_events(...)` | Function that fans an event out to every subscription |

Publishing an event calls the `publish_events` function, which inserts the message into each
subscription's table. So an event with three subscriptions is stored three times, and each subscriber
consumes independently.

The connection is registered as a keyed `NpgsqlDataSource` under the key `knightbus-postgres`, so it
does not collide with your application's own data source registration.

## Sagas

```csharp
services.UsePostgresSagaStore();
```

State goes in `knightbus.sagas`, created on demand.

!!! warning "No concurrent-write detection"
    This store overwrites unconditionally — it does not check `ConcurrencyStamp`, so simultaneous
    updates to the same saga silently lose one another. Serialize the saga with
    `MaxConcurrentCalls => 1` or use the Blob store. See
    [saga concurrency](../features/sagas.md#concurrency).

## Limitations

Two behaviours specific to this transport are worth knowing before you choose it:

!!! warning "No attachments, no outgoing trace propagation"
    `PostgresBus` does not run [message pre-processors](../concepts/messages.md#pre-processing-messages-on-send).
    Anything built on them has no effect when sending over PostgreSQL:

    - **[Attachments](../features/attachments.md)** are never uploaded.
    - **Outgoing [distributed tracing](../monitoring.md#distributed-tracing)** properties are not
      attached.

    Custom pre-processors you register are also skipped for these messages.

!!! warning "`ICustomMessageSerializer` is ignored when sending"
    The serializer is captured when the client is constructed, so a per-message override on the
    mapping applies on receive but not on send — an asymmetry that would corrupt round-trips. Set the
    serializer on the transport configuration instead.

## Management

```csharp
services.UsePostgresManagement(connectionString);
```

There is also `UsePostgresManagementWithAzureManagedIdentity` in
`KnightBus.PostgreSql.Management.Extensions.Azure`.

`PeekScheduled` and `CancelScheduledMessage` throw `NotSupportedException`. Dead letter rows carry no
delivery count, so `DeliveryCount` always reads `0` for them; the failure reason is available as the
`error_message` property.

## Serialization

**PostgreSQL is the only transport defaulting to `System.Text.Json`** rather than Newtonsoft. If you
move a message contract to or from this transport, check that its serialized shape survives the
change — see [serialization](../concepts/serialization.md#defaults-differ-per-transport).

## Example

[`KnightBus.Examples.PostgreSql`](https://github.com/BookBeat/knightbus/tree/master/knightbus/examples/KnightBus.Examples.PostgreSql)
covers commands, events with two subscriptions, a poison message, a saga and a custom shutdown grace
period.
