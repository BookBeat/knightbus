# Marker interfaces

KnightBus is configured almost entirely by which interfaces your types implement. Very little
behaviour comes from configuration files or attributes — you opt in by adding an interface, and the
framework finds it.

That makes one question worth answering precisely: **which type do I put the interface on?** Putting
the right interface on the wrong type is the most common cause of "I added it and nothing happened",
because a marker in the wrong place is simply never looked for.

There are four places, and this page is organised by them.

| Put it on the… | …to control |
| --- | --- |
| [Message](#on-the-message) | Which transport carries it, and whether it has an attachment |
| [Mapping](#on-the-mapping) | Its queue name, its serializer, how its entity is created |
| [Processor](#on-the-processor) | What handles it, and lifecycle hooks around that |
| [Processing settings](#on-the-processing-settings) | Concurrency, timeouts and retries |

---

## On the message

The message contract itself. Its interface decides the transport — this is the only routing mechanism
in KnightBus.

| Interface | Effect | Notes |
| --- | --- | --- |
| `IMessage` | Base for everything KnightBus processes. | Never implement directly. |
| `ICommand` | One logical consumer. | Implement a transport-specific descendant instead. |
| `IEvent` | Fan-out to many subscriptions. | Implement a transport-specific descendant instead. |
| `IRequest` | Request/response. | Implement `INatsRequest`; only NATS supports requests. |
| `ICommandWithAttachment` | Adds an out-of-band payload. | Requires a registered attachment provider on **both** sender and receiver. Adds `IMessageAttachment Attachment { get; set; }`. |

### Transport interfaces

| Transport | Command | Event | Request |
| --- | --- | --- | --- |
| [Azure Service Bus](../transports/azure-service-bus.md) | `IServiceBusCommand` | `IServiceBusEvent` | — |
| [Azure Storage Queues](../transports/azure-storage-queues.md) | `IStorageQueueCommand` | — | — |
| [PostgreSQL](../transports/postgresql.md) | `IPostgresCommand` | `IPostgresEvent` | — |
| [Redis](../transports/redis.md) | `IRedisCommand` | `IRedisEvent` | — |
| [NATS](../transports/nats.md) | `INatsCommand` | `INatsEvent` | `INatsRequest` |

```csharp
public class ImportFile : IServiceBusCommand, ICommandWithAttachment
{
    public string Description { get; set; }
    public IMessageAttachment Attachment { get; set; }
}
```

A message whose transport is not registered fails at **host startup** with
`No transport found for {type}, did you forget to register it?`.

---

## On the mapping

The `IMessageMapping<T>` class for a message. Mappings are found by scanning the assembly that
declares the message, which is why per-message configuration lives here rather than on the message.

| Interface | Required? | Effect |
| --- | --- | --- |
| `IMessageMapping<T>` | **Yes** | Names the queue or topic. Must be in the same assembly as `T`. |
| `ICustomMessageSerializer` | No | Overrides the serializer for this message only. Ignored when sending over PostgreSQL. |
| `IServiceBusCreationOptions` | No | Overrides partitioning, ordering and batching when the Service Bus entity is created. All three values must be supplied; applies at creation only. |

```csharp
public class ImportFileMapping
    : IMessageMapping<ImportFile>,
        ICustomMessageSerializer,
        IServiceBusCreationOptions
{
    public string QueueName => "import-file";

    public IMessageSerializer MessageSerializer => new MicrosoftJsonSerializer();

    public bool EnablePartitioning => true;
    public bool SupportOrdering => false;
    public bool EnableBatchedOperations => true;
}
```

A missing mapping throws `MessageMappingMissingException` — `No queue name mapping exists for {type}`.

### Event subscriptions

An event also needs one subscription type per independent listener. It is its own class, not a marker
on anything else:

| Interface | Effect |
| --- | --- |
| `IEventSubscription<T>` | Declares one named subscription to event `T`. Needs a public parameterless constructor. |

```csharp
public class InvoicingSubscription : IEventSubscription<OrderPlaced>
{
    public string Name => "invoicing";
}
```

---

## On the processor

The handler class. This is where most behaviour hooks live.

### Handler interfaces

Exactly one of these makes a class a message processor. It must also be registered, with
`RegisterProcessors()` or `RegisterProcessor<T>()`.

| Interface | Handles | Returns |
| --- | --- | --- |
| `IProcessCommand<T, TSettings>` | A command | `Task` |
| `IProcessEvent<TTopic, TTopicSubscription, TSettings>` | An event on one subscription | `Task` |
| `IProcessRequest<TRequest, TResponse, TSettings>` | A request, one reply | `Task<TResponse>` |
| `IProcessStreamRequest<TRequest, TResponse, TSettings>` | A request, many replies | `IAsyncEnumerable<TResponse>` |
| `IProcessSchedule<T>` | A cron occurrence — not a message | `Task` |

A class may implement several. Processors are resolved per message as **scoped** services.

### Behaviour hooks

These change how the framework treats the processor. All go on the same class as the handler.

| Interface | Effect | Prerequisites |
| --- | --- | --- |
| `ISingletonProcessor` | One execution at a time across all instances. Forces `MaxConcurrentCalls` to 1 and `PrefetchCount` to 0. | A registered `ISingletonLockManager`, or the host fails to start. |
| `IProcessBeforeDeadLetter<T>` | Hook invoked immediately before a message is dead-lettered. | Must be on the same class and closed over the same `T` as the handler. Cannot veto dead-lettering; its exceptions are swallowed. |
| `ISagaDuplicateDetected<T>` | Hook invoked when a start message arrives for a saga that already exists. | Only meaningful on a saga. The message is completed afterwards regardless. |
| `Saga<TData>` (base class) | Makes the processor a stateful saga. Requires overriding `PartitionKey` and `TimeToLive`, and mapping messages in the constructor. | A registered `ISagaStore` via `EnableSagas`. `TData` needs a parameterless constructor. |

```csharp
public class ImportProcessor
    : IProcessCommand<ImportFile, DefaultSettings>,
        ISingletonProcessor,
        IProcessBeforeDeadLetter<ImportFile>
{
    public Task ProcessAsync(ImportFile message, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task BeforeDeadLetterAsync(ImportFile message, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
```

---

## On the processing settings

The settings class named as the processor's last type parameter. It must be a class with a
parameterless constructor.

| Interface | Required? | Effect |
| --- | --- | --- |
| `IProcessingSettings` | **Yes** | `MaxConcurrentCalls`, `PrefetchCount`, `MessageLockTimeout`, `DeadLetterDeliveryLimit`. |
| `IExtendMessageLockTimeout` | No | Renews the transport lock while the handler runs, via `ExtensionDuration` and `ExtensionInterval`. |

!!! warning "`IExtendMessageLockTimeout` needs two more things"
    Implementing it is not sufficient. You must also register the middleware yourself —
    `services.AddMiddleware<ExtendMessageLockDurationMiddleware>()` — and the transport must support
    changing a lock mid-flight, which only **Azure Storage Queues** does.

    On **PostgreSQL** it is actively harmful rather than inert: the fetch lock is shortened to
    `ExtensionDuration` with nothing able to renew it, causing duplicate processing. See
    [long-running work](../concepts/processors.md#long-running-work-extending-the-lock).

Schedules are configured by their own type rather than by settings:

| Interface | Effect |
| --- | --- |
| `ISchedule` | Declares a cron expression and time zone. Instantiated reflectively, so it needs a public parameterless constructor. Invalid expressions fail host startup. |

---

## Extension points

These are not markers you add to an existing type — they are contracts you implement in a new class
and register. Listed here so the whole extensible surface is in one place.

### Pipeline

| Interface | Purpose | Registration |
| --- | --- | --- |
| `IMessageProcessorMiddleware` | Wrap message processing. | `AddMiddleware<T>()` |
| `IMessageScopeProviderMiddleware` | Replace the per-message DI scope. At most one. | `AddMiddleware<T>()` |
| `IMessagePreProcessor` | Add properties to outgoing messages. | `AddScoped`/`AddSingleton` |

Supporting types a middleware receives: `IMessageStateHandler<T>` (the message's handle on the
transport), `IPipelineInformation` (what listener it arrived on), `IMessageProcessor` (the next step),
and `IMessageLockHandler<T>` where the transport supports lock renewal.

### Storage and providers

| Interface | Purpose | Registration |
| --- | --- | --- |
| `IMessageSerializer` | Serialize message bodies. | Transport config, or `ICustomMessageSerializer` |
| `IMessageAttachmentProvider` | Store and retrieve attachments. | `AddSingleton` + `AttachmentMiddleware` |
| `ISagaStore` | Persist saga state. | `EnableSagas<T>()` |
| `ISingletonLockManager` / `ISingletonLockHandle` | Distributed locking. | `UseSingletonLocks(...)` |
| `IBlobLockScheme` | Where the Blob lock manager keeps its leases. | `UseBlobStorageLockManager(scheme)` |
| `IDistributedTracingProvider` | Carry correlation across messages. | `UseDistributedTracing<T>()` |

### Host

| Interface | Purpose | Registration |
| --- | --- | --- |
| `IPlugin` | Background component started with the host. | `AddPlugin<T>()` |
| `IStoppablePlugin` | A plugin that shuts down cleanly. | `AddPlugin<T>()` |
| `IHostConfiguration` | Host settings, notably `ShutdownGracePeriod`. | `UseKnightBus(config => ...)` |
| `ITcpAliveListenerConfiguration` | Liveness probe port. | `UseTcpAliveListener(...)` |

### Transports and management

| Interface | Purpose |
| --- | --- |
| `ITransport`, `ITransportChannelFactory`, `IChannelReceiver` | Implement a new transport. |
| `ITransportConfiguration` | A transport's connection string and serializer. |
| `IQueueManager` | Inspect and manage queues. |
| `IQueueMessageSender` | Send raw messages; cancel scheduled ones. |
| `IQueueMessageAttachmentProvider` | Read attachments of inspected messages. |

---

## Things that look like markers but are not

A few behaviours are enabled only by a registration call, with no interface to add. If you are
searching for an interface to make one of these happen, there isn't one:

| Behaviour | How to enable |
| --- | --- |
| Host-wide concurrency limit | `ThrottleHost(maxConcurrent)` |
| Liveness probe | `UseTcpAliveListener(port)` |
| Cron scheduling | `UseScheduling()` + `RegisterSchedules()` |
| Attachments | `UseBlobStorageAttachments()` |
| Sagas | `UseBlobStorageSagas()` / `UseRedisSagaStore()` / `UsePostgresSagaStore()` / `UseSqlServerSagaStore(...)` |
| Distributed tracing | `UseDistributedTracing()` |
| Telemetry | `UseOpenTelemetry()` / `UseApplicationInsights(...)` / `UseNewRelic()` |
| Queue management | `AddServiceBusManagement(...)` and friends |

Where a row lists several calls, they are alternative *stores*, not one call per transport:
`UseBlobStorageAttachments()` and `UsePostgresSagaStore()` are as valid in a Service Bus application
as anywhere else. Only queue management is transport-specific. See the
[transport matrix](../transports/index.md#feature-matrix).

## Quick diagnosis

| Symptom | Likely cause |
| --- | --- |
| Handler never runs, no error | Processor not registered, or `RegisterProcessors()` scanned the wrong assembly. |
| `No transport found for {type}` at startup | Missing `UseTransport<T>()` for that message's transport. |
| `No queue name mapping exists for {type}` | Mapping missing, or not in the same assembly as the message. |
| Host fails: no `ISingletonLockManager` | `ISingletonProcessor` or `UseScheduling()` without a lock manager. |
| Lock extension has no effect | Middleware not registered, or transport is not Azure Storage Queues. |
| Attachment is null on receive | No attachment provider on the receiving host. |
| Dead letter hook never fires | Transport's own delivery limit is lower than `DeadLetterDeliveryLimit`. |
| `WRONGTYPE` from the Redis saga store | Saga keys written by KnightBus.Redis 15.x or earlier; delete `sagas:*` before upgrading. |
| Custom serializer ignored | Placed on the message or processor instead of the mapping — or sending over PostgreSQL. |
| `An instance of ISagaStore is already registered` | Two saga stores registered; only one is allowed. |
| Events not received by a new subscriber (Redis) | Fan-out happens at publish time; the subscription must exist before events are published. |
