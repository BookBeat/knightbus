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

| | [Service Bus](azure-service-bus.md) | [Storage Queues](azure-storage-queues.md) | [PostgreSQL](postgresql.md) | [Redis](redis.md) | [NATS](nats.md) |
| --- | :--: | :--: | :--: | :--: | :--: |
| Commands | ✅ | ✅ | ✅ | ✅ | ✅ |
| Events (pub/sub) | ✅ | — | ✅ | ✅ | ✅ |
| Request/response | — | — | — | — | ✅ |
| Streaming responses | — | — | — | — | ✅ |
| Deferred delivery | ✅ | ✅ | ✅ | — | — |
| Cancel deferred message | ✅ | — | — | — | — |
| Attachments | ✅ | ✅ | — | ✅ | ✅ |
| Saga store | — | ✅ | ✅ | ✅ | — |
| Singleton lock manager | — | ✅ | — | — | — |
| Dead letter queue | ✅ | ✅ | ✅ | ✅ | — |
| Management API | ✅ | ✅ | ✅ | ✅ | — |
| Message lock extension | — | ✅ | — | — | — |
| Default serializer | Newtonsoft | Newtonsoft | System.Text.Json | Newtonsoft | Newtonsoft |

Two rows need explanation, and they mean different things.

**Attachments** ✅ means "attachments work on this transport", not "this package provides the
storage". Only two packages ship an `IMessageAttachmentProvider` — `KnightBus.Azure.Storage` (Blob,
the usual choice) and `KnightBus.Redis` — and either can back attachments on any transport. So a
message travelling over NATS with its payload in Blob Storage is a normal arrangement. The PostgreSQL
✗ is genuine, though: that transport does not run the pre-processor that uploads attachments, so they
never get stored.

**Saga store** ✅ means the package ships an `ISagaStore` implementation. Saga state is likewise
independent of the transport carrying the messages — see [sagas](../features/sagas.md) for the
important differences between the stores, particularly that only the Blob store detects concurrent
writes.

**SQL Server** appears nowhere above because `KnightBus.SqlServer` is a
[saga store](../features/sagas.md) only, not a transport.

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
