# Middleware pipeline

Every message passes through a pipeline of middleware before reaching your processor. Much of
KnightBus itself is built this way — error handling, dead-lettering, attachments and sagas are all
middleware — and you extend it with the same interface.

```csharp
public interface IMessageProcessorMiddleware
{
    Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
        where T : class, IMessage;
}
```

## Pipeline order

The order is **fixed**, not the order you register in. A pipeline is built per listener, outermost
first:

1. **In-flight tracker** — counts messages so [shutdown](host.md#shutdown) can drain. Always
   outermost, so the count covers everything below it.
2. **`ErrorHandlingMiddleware`** — catches every exception from everything inside it, logs it, and
   marks the message failed so the transport can redeliver.
3. **The scope provider** — creates the per-message DI scope.
4. **`DeadLetterMiddleware`** — dead-letters messages that have exhausted their delivery attempts.
5. **Your middleware**, and KnightBus' optional middleware, in registration order.
6. **Your processor.**

!!! warning "Registered middleware runs innermost, not outermost"
    Anything you add with `AddMiddleware` runs *inside* error handling and dead-lettering, closest to
    the handler. Relative order among your own middleware follows registration order, but you cannot
    place your middleware outside the built-in ones. In particular, a middleware that wants to
    observe exceptions must catch them itself — `ErrorHandlingMiddleware` sits further out and has
    already swallowed anything that reached it.

## Writing middleware

Call `next.ProcessAsync` to continue down the pipeline. Skip the call to stop the message going any
further.

```csharp
public class PerformanceLogging : IMessageProcessorMiddleware
{
    private readonly ILogger<PerformanceLogging> _logger;

    public PerformanceLogging(ILogger<PerformanceLogging> logger) => _logger = logger;

    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _logger.LogInformation(
                "{MessageType} took {Elapsed}ms",
                typeof(T).Name,
                stopwatch.ElapsedMilliseconds
            );
        }
    }
}
```

Register it:

```csharp
services.AddMiddleware<PerformanceLogging>();       // resolved from DI, singleton
services.AddMiddleware(new PerformanceLogging(logger)); // or a ready-made instance
```

!!! note "Middleware is a singleton"
    One instance is shared by every listener and every message, concurrently. Keep it thread-safe and
    put per-message state in the DI scope rather than in fields.

### What you get to work with

`IPipelineInformation` describes the listener the message arrived on:

| Member | Use |
| --- | --- |
| `ProcessorInterfaceType` | The closed processor interface, e.g. `IProcessCommand<SampleCommand, DefaultSettings>`. |
| `Subscription` | The event subscription, or `null` for commands. |
| `ProcessingSettings` | The settings instance for this listener. |
| `HostConfiguration` | Host-level configuration, including the logger. |

`IMessageStateHandler<T>` is the message's handle on the transport:

| Member | Use |
| --- | --- |
| `GetMessage()` | Deserialize and return the message. |
| `DeliveryCount` | How many times this message has been picked up. |
| `DeadLetterDeliveryLimit` | The limit from the listener's settings. |
| `MessageProperties` | Transport properties travelling with the message. |
| `MessageScope` | The per-message DI scope. |
| `CompleteAsync()` | Mark as successfully handled. |
| `AbandonByErrorAsync(e)` | Mark as failed, making it available for redelivery. |
| `DeadLetterAsync(limit)` | Move it to the dead letter queue. |
| `ReplyAsync<TReply>(reply)` | Reply to the caller, for request/response. |

## The scope provider

The middleware at position 3 creates the DI scope for the message. By default this is
`MicrosoftDependencyInjectionScopedLifeStyleMiddleware`, which opens a scope per message and disposes
it afterwards.

To take over, register an `IMessageScopeProviderMiddleware`:

```csharp
public class MyScopeProvider : IMessageScopeProviderMiddleware { /* ... */ }

services.AddMiddleware<MyScopeProvider>();
```

!!! warning "Only one scope provider"
    Registering two `IMessageScopeProviderMiddleware` implementations throws while the pipeline is
    being built at host startup. It is hoisted out of the ordinary ordering to position 3 so that
    everything below it — including dead-lettering — can resolve services from the message scope.

## Built-in middleware

| Middleware | Added by | What it does |
| --- | --- | --- |
| `ErrorHandlingMiddleware` | always | Catches, logs, and abandons the message on failure. |
| `DeadLetterMiddleware` | always | Dead-letters once delivery attempts are exhausted, via the [`IProcessBeforeDeadLetter<T>`](../features/error-handling.md#hooking-into-dead-lettering) hook. |
| Scope provider | always | One DI scope per message. |
| `AttachmentMiddleware` | `UseBlobStorageAttachments()` | Loads and cleans up [attachments](../features/attachments.md). |
| `SagaMiddleware` | `EnableSagas(...)`, `UseBlobStorageSagas()`, `UsePostgresSagaStore()`, `UseRedisSagaStore()`, `UseSqlServerSagaStore(...)` | Loads and persists [saga](../features/sagas.md) state. |
| `DistributedTracingMiddleware` | `UseDistributedTracing()` | Restores the incoming trace id into the message scope. |
| `ThrottlingMiddleware` | `ThrottleHost(n)` | Host-wide concurrency gate. |
| `ExtendMessageLockDurationMiddleware` | **manual** `AddMiddleware<...>()` | Renews the transport lock for [long-running work](processors.md#long-running-work-extending-the-lock), on transports that can renew one. |
| `OpenTelemetryMessageMiddleware` | `UseOpenTelemetry()` | Emits spans — see [monitoring](../monitoring.md). |
| `ApplicationInsightsMessageMiddleware` | `UseApplicationInsights(...)` | Application Insights telemetry. |
| `NewRelicMessageMiddleware` | `UseNewRelic()` | New Relic transactions. |

!!! note "A `Use…` call names a store, not a transport"
    Every middleware above ships in `KnightBus.Core` or a monitoring package — **no transport package
    contains one**. `UseBlobStorageAttachments()` registers the Blob-backed attachment store and the
    core `AttachmentMiddleware`; it does not make attachments a Storage Queues feature, and it does
    not require that transport. The same goes for the saga stores and the Blob lock manager.
    Middleware is registered per host and runs on **every** listener, whatever transport the message
    arrived on, so attachments over NATS backed by Blob Storage — or any other combination — need no
    special handling.

    One of them does depend on the transport for its *effect*, not its registration:
    `ExtendMessageLockDurationMiddleware` only renews a lock when the message state handler
    implements `IMessageLockHandler<T>` (today only Storage Queues).
    See the [transport matrix](../transports/index.md#feature-matrix).

## See also

- [Host and configuration](host.md) — where middleware is registered.
- [Errors and dead-lettering](../features/error-handling.md) — the behaviour of the two mandatory
  middlewares.
