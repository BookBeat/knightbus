# Redis

The highest-throughput KnightBus transport: commands and events built on Redis lists. The package
also ships an attachment provider and a saga store, which are host-wide features rather than Redis
transport features — any transport can use them, and this transport can equally use the ones shipped
by other packages.

```bash
dotnet add package KnightBus.Redis
dotnet add package KnightBus.Redis.Messages
```

## Registration

```csharp
services
    .UseRedis(config =>
    {
        config.ConnectionString = "localhost:6379";
        config.DatabaseId = 0;
    })
    .RegisterProcessors()
    .UseTransport<RedisTransport>();
```

The connection multiplexer is registered as a singleton and shared, as StackExchange.Redis intends.

## Messages

| Interface | Kind |
| --- | --- |
| `IRedisCommand` | Command |
| `IRedisEvent` | Event |

## Client

```csharp
var bus = scope.ServiceProvider.GetRequiredService<IRedisBus>();

await bus.SendAsync(new ThumbnailRequested { ImageId = "1" });
await bus.SendAsync(new[] { command1, command2, command3 });   // batched
await bus.PublishAsync(new CacheInvalidated());
```

!!! note "No cancellation tokens, no scheduling"
    `IRedisBus` is the only client that takes no `CancellationToken` on any method, and Redis has no
    deferred delivery. For delayed messages use
    [Service Bus, Storage Queues or PostgreSQL](../features/scheduling.md#deferred-messages).

## How it works

**Commands** use a circular-list pattern. A message is pushed onto the queue list, and when a consumer
picks it up it is atomically moved to `{queue}:processing` rather than deleted. If the consumer dies
mid-processing the message is still on that list and is recovered, so messages are not lost in
transit. Failed messages end up on `{queue}:deadletter`.

**Events** are fanned out at publish time. The client looks up which subscriptions exist and pushes a
copy to each one's list, so an event with three listeners is written to three lists. Each subscriber
then consumes at its own pace.

Because the fan-out happens on the publisher, a subscription that does not exist yet when an event is
published does not receive that event. Deploy the subscriber before the events start flowing.

## Throughput

Redis is the transport to reach for when volume matters, and the settings that matter most are
`MaxConcurrentCalls` and `PrefetchCount`:

```csharp
public class HighThroughputSettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 1000;
    public int PrefetchCount => 1000;
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);
    public int DeadLetterDeliveryLimit => 5;
}
```

The Redis example pushes ten thousand messages through settings like these. Prefetching aggressively
is safe here precisely because of the circular-list recovery — an interrupted consumer's prefetched
messages are not stranded.

## Attachments

```csharp
services
    .UseRedis(config => config.ConnectionString = connectionString)
    .UseRedisAttachments();
```

Attachments are stored in Redis itself, uncompressed — there is no options overload.

This registers a *provider*, not a Redis-transport feature. `UseRedisAttachments()` backs
[attachments](../features/attachments.md) for every transport in the host — a Service Bus or NATS
message can carry its payload in Redis — and it needs `UseRedis(...)` for the connection, not
`UseTransport<RedisTransport>()`. The reverse holds too: messages travelling over Redis can use the
[Blob Storage provider](azure-storage-queues.md#attachments) instead, which is the better choice for
large or long-lived attachments since Redis keeps everything in memory.

## Saga store

```csharp
services.UseRedisSagaStore();
```

Like the attachment provider, this is independent of the transport: it stores
[saga](../features/sagas.md) state for messages arriving on any transport, and sagas over Redis can
just as well use the Blob, PostgreSQL or SQL Server store.

Each saga is a Redis hash at `sagas:{partitionKey}:{id}` with two fields: `data` holds the serialized
state and `stamp` the concurrency stamp. `Create` sets the saga's `TimeToLive` as the key expiry, and
because updates only rewrite hash fields the expiry is preserved until the saga completes or Redis
expires it.

Concurrent writes are detected. `Create` and `GetSaga` return a `ConcurrencyStamp`, and an `Update`
or `Complete` whose stamp no longer matches throws `SagaDataConflictException`, which retries the
message — see [saga concurrency](../features/sagas.md#concurrency). Each write is a single Lua script
on a single key, so the check and the write are atomic and the store works unchanged on Redis
Cluster. The server must allow `EVAL`, `EVALSHA` and `SCRIPT LOAD`. A `CancellationToken` is honoured
before a call reaches Redis, not during it — StackExchange.Redis does not take one.

!!! warning "Upgrading from 15.x"
    Versions before 16.0.0 stored each saga as a plain string under the same key. A 16.x host reading
    such a key fails with `WRONGTYPE`, and a 15.x host reading a 16.x hash fails the same way. Let
    running sagas finish, or delete `sagas:*`, before upgrading, and do not run both versions against
    one Redis database.

## Management

```csharp
services.UseRedisManagement(connectionString, databaseId: 0);
```

`PeekScheduled` is not supported, and this transport registers no `IQueueMessageSender`, so sending
messages through the management API is unavailable.

## Serialization

Defaults to `NewtonsoftSerializer`.

## Example

[`KnightBus.Examples.Redis`](https://github.com/BookBeat/knightbus/tree/master/knightbus/examples/KnightBus.Examples.Redis)
covers commands, events with three subscriptions, attachments, a saga and a custom performance-logging
middleware. Start Redis with `docker run -p 6379:6379 redis`.
