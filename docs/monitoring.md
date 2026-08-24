# Monitoring

KnightBus emits telemetry through pluggable integrations. Each one is a middleware that wraps message
processing, so it sees every message on every transport.

| Integration | Package | Registration |
| --- | --- | --- |
| OpenTelemetry | `KnightBus.OpenTelemetry` | `services.UseOpenTelemetry()` |
| Application Insights | `KnightBus.ApplicationInsights` | `services.UseApplicationInsights(telemetryConfiguration)` |
| New Relic | `KnightBus.NewRelic` | `services.UseNewRelic()` |

OpenTelemetry is the recommended choice for new applications.

## OpenTelemetry

Two calls are needed: one to make KnightBus emit activities, one to make OpenTelemetry collect them.

```csharp
services.UseOpenTelemetry();

services
    .AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddKnightBusInstrumentation()
        .AddOtlpExporter());
```

`UseOpenTelemetry()` registers the message-processing middleware and an OpenTelemetry-backed
distributed tracing provider. `AddKnightBusInstrumentation()` adds KnightBus' `ActivitySource` —
named `KnightBus` — to the tracer provider.

!!! note
    `UseOpenTelemetry()` already configures distributed tracing. Do not also call
    `UseDistributedTracing()`; the two would register competing providers.

You get:

- a span per processed message,
- W3C Trace Context propagation across message boundaries,
- exceptions recorded with stack traces,
- message properties as span attributes.

### Spans and attributes

Each span is named after the message type's full name and has kind `Consumer`. Its parent context is
extracted from the incoming message properties, which is what stitches a chain of handlers into one
trace.

| Attribute | Value |
| --- | --- |
| `messaging.system` | `knightbus` |
| `messaging.operation` | `process` |
| `messaging.destination.name` | Full type name of the message |
| `messaging.destination.queue.name` | Queue name, for commands |
| `messaging.destination.topic.name` | Topic name, for events |
| `messaging.destination.subscription.name` | Subscription name, for events |
| `messaging.knightbus.*` | Message properties |

Successful processing sets status `Ok`. A thrown exception is recorded on the span with its stack
trace, the status is set to `Error`, and the exception is rethrown so normal
[retry and dead-lettering](features/error-handling.md) still apply.

When no listener is registered, no activity is created and the instrumentation costs nothing.

### Zero-code instrumentation

With the
[OpenTelemetry .NET Automatic Instrumentation](https://opentelemetry.io/docs/zero-code/dotnet/),
register the source through the environment instead:

```bash
export OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_SOURCES="KnightBus"
```

## Application Insights

```csharp
services.UseApplicationInsights(telemetryConfiguration);
```

This registers the telemetry middleware and turns on dependency tracking, including Service Bus and
Event Hubs diagnostic sources.

Live Metrics Stream is a separate opt-in, and — unlike the others — it extends `IHostConfiguration`
rather than `IServiceCollection`, so it goes inside the `UseKnightBus` callback:

```csharp
.UseKnightBus(config => config.EnableLiveMetricsStream(telemetryConfiguration))
```

## New Relic

```csharp
services.UseNewRelic();
```

Each processed message becomes a New Relic transaction. Like `UseOpenTelemetry()`, this also
configures distributed tracing with its own provider, so do not call `UseDistributedTracing()`
alongside it.

## Distributed tracing

Independently of any vendor integration, KnightBus can carry a trace id from message to message so a
chain of handlers can be correlated:

```csharp
services.UseDistributedTracing();
```

The default provider propagates a `_traceid` message property, minting a new id when an incoming
message has none. Read it in a processor by injecting the provider:

```csharp
public class SampleProcessor : IProcessCommand<SampleCommand, DefaultSettings>
{
    private readonly IDistributedTracingProvider _tracing;

    public SampleProcessor(IDistributedTracingProvider tracing) => _tracing = tracing;

    public Task ProcessAsync(SampleCommand message, CancellationToken cancellationToken)
    {
        var traceId = _tracing.GetProperties()[DistributedTracingUtility.TraceIdKey];
        return Task.CompletedTask;
    }
}
```

To integrate with a different correlation scheme, implement `IDistributedTracingProvider` and
register it with `UseDistributedTracing<MyProvider>()`.

!!! warning "Not propagated by the PostgreSQL transport"
    Outgoing trace properties are attached by a pre-processor, and `PostgresBus` does not run
    pre-processors — so trace context is not propagated when sending over PostgreSQL.

## Liveness probes

`UseTcpAliveListener(port)` opens a TCP port for orchestrator health checks. It stops answering as
soon as shutdown begins, so an instance is taken out of rotation while it drains. See
[host and configuration](concepts/host.md#liveness-probes).

## Logging

KnightBus logs through `Microsoft.Extensions.Logging` using whatever the generic host is configured
with. Notable messages:

| Log | Meaning |
| --- | --- |
| `Error processing message {...}` | A handler threw; the message will be retried. |
| `Failed before deadletter processing {...}` | An `IProcessBeforeDeadLetter<T>` hook threw. The message is still dead-lettered. |
| `KnightBus shutdown proceeding with {N} messages still processing` | The [grace period](concepts/host.md#shutdown) expired before the drain finished. |
| `KnightBus shutdown proceeding before all singleton locks were released and plugins stopped` | Teardown budget expired; locks will expire on their own. |
| `Setting {Name} in Singleton mode` | A processor was detected as `ISingletonProcessor`. |

The last two are the ones worth alerting on — they indicate shutdowns that are not clean, which
during a rolling deploy means duplicated or delayed processing.

## What to watch

- **Dead letter depth** — every message there needs a human. See
  [errors and dead-lettering](features/error-handling.md).
- **Queue depth and age** — read with the [management API](features/management.md).
- **Handler duration against `MessageLockTimeout`** — approaching the limit means duplicate
  processing is imminent.
- **Shutdown warnings** — as above.
