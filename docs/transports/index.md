# Transports

A transport determines how a message physically travels from sender to processor. KnightBus' defining
characteristic is that the transport is chosen **per message type**, by the interface the message
implements, and that any number of transports can be active in the same host.

```csharp
services
    .UseServiceBus(config => config.ConnectionString = serviceBusConnection)
    .UseTransport<ServiceBusTransport>()
    .UseRedis(config => config.ConnectionString = redisConnection)
    .UseTransport<RedisTransport>()
    .RegisterProcessors();
```

With that in place, an `IServiceBusCommand` goes to Service Bus and an `IRedisCommand` goes to Redis,
with no routing configuration anywhere.

## Feature matrix

What the transport itself can do:

| | [Service Bus](azure-service-bus.md) | [Storage Queues](azure-storage-queues.md) | [PostgreSQL](postgresql.md) | [Redis](redis.md) | [NATS](nats.md) |
| --- | :--: | :--: | :--: | :--: | :--: |
| Commands | ✅ | ✅ | ✅ | ✅ | ✅ |
| Events (pub/sub) | ✅ | — | ✅ | ✅ | ✅ |
| Request/response | — | — | — | — | ✅ |
| Streaming responses | — | — | — | — | ✅ |
| Deferred delivery | ✅ | ✅ | ✅ | — | — |
| Cancel deferred message | ✅ | — | — | — | — |
| Dead letter queue | ✅ | ✅ | ✅ | ✅ | — |
| Management API | ✅ | ✅ | ✅ | ✅ | — |
| Message lock extension | — | ✅ | — | — | — |
| Carries attachments | ✅ | ✅ | ✅ | ✅ | ✅ |
| Default serializer | Newtonsoft | Newtonsoft | System.Text.Json | Newtonsoft | Newtonsoft |

What each package *ships* — a different question, answered by a different table:

| | Service Bus | Storage Queues | PostgreSQL | Redis | NATS | [SQL Server](../features/sagas.md) |
| --- | :--: | :--: | :--: | :--: | :--: | :--: |
| Attachment provider | — | ✅ Blob | — | ✅ | — | — |
| Saga store | — | ✅ Blob | ✅ | ✅ | — | ✅ |
| Singleton lock manager | — | ✅ Blob lease | — | — | — | — |

### The second table is not a restriction

Nothing in the second table belongs to the transport it ships beside. Attachments, sagas and
singleton locks are host-wide features whose middleware lives in `KnightBus.Core`, and they are
configured **once per host, not per transport**. A `UseXxxAttachments()` or `UseXxxSagaStore()` call
names the *storage* backing the feature; it says nothing about which transport your messages travel
over. `KnightBus.SqlServer` is the case that makes this obvious — a saga store with no transport at
all.

So all of these are ordinary arrangements, not workarounds:

- messages over NATS, attachments in Blob Storage, saga state in PostgreSQL;
- messages over Service Bus with singleton processors, coordinated by Blob leases — the only lock
  implementation KnightBus ships, and the reason `KnightBus.Azure.Storage` appears in applications
  that use no Azure queue at all;
- messages over Redis with attachments in Blob Storage, because Redis keeps its own in memory.

Pick each row of the second table on its own merits — cost, durability, what you already operate —
and mix freely. The differences that matter are between the *implementations*, not the transports:
see [sagas](../features/sagas.md#concurrency) for the important one, that only the Blob store detects
concurrent writes.

### Where the transport does constrain you

One row in the first table is a real coupling.

**Message lock extension** is about the receive side. `ExtendMessageLockDurationMiddleware` is a core
middleware you may register on any host, but it can only renew a lock the transport lets it renew:
the message state handler has to implement `IMessageLockHandler<T>`, which today only Storage Queues
does. Elsewhere the middleware is inert — and on PostgreSQL, implementing `IExtendMessageLockTimeout`
is worse than inert, see
[extending the lock](../concepts/processors.md#long-running-work-extending-the-lock).

## Choosing one

- **Azure Service Bus** — the most capable option. Native dead-lettering, topics with subscriptions,
  deferred and cancellable messages. Choose it when you want the broker to do the work and you are
  already on Azure.
- **Azure Storage Queues** — cheap, simple and durable, and the only transport that can extend a
  message lock mid-processing. Good for long-running low-throughput work. Commands only.
- **PostgreSQL** — no extra infrastructure if you already run Postgres, and transactional with your
  own data. Polling-based, so latency is bounded by the polling delay.
- **Redis** — the highest-throughput option, using a circular-list pattern so messages are not lost
  in transit. Choose it for high-volume work where a few seconds of durability risk is acceptable.
- **NATS** — the only transport with request/response and streaming responses. Choose it when you
  need a reply, not just a hand-off.

## Common shape

Every transport follows the same registration pattern — three calls, of which the first is named after
the transport:

```text
services
    .UseXxx(config => config.ConnectionString = connectionString)   // client + configuration
    .UseTransport<XxxTransport>()                                  // start listeners
    .RegisterProcessors();                                         // find handlers
```

`UseXxx` registers the configuration and the bus client. `UseTransport<T>` starts the
listeners. Forgetting the second one is the most common setup mistake — the client works, messages are
sent, and nothing is ever consumed.

Queue and topic entities are created automatically on first use by every transport.

## Adding your own

The transport SPI is `ITransport` plus `ITransportChannelFactory`, and for polling transports
`GenericMessagePump` handles the prefetch and concurrency arithmetic. See
[contributing](../contributing.md#adding-a-transport).
