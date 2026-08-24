# Host and configuration

The KnightBus host connects to the transports, listens for messages and invokes your processors. It
is an `IHostedService` on the standard .NET generic host, so it lives alongside whatever else your
application runs.

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services
            .UseServiceBus(config => config.ConnectionString = connectionString)
            .RegisterProcessors()
            .UseTransport<ServiceBusTransport>();
    })
    .UseKnightBus()
    .Build();

await host.RunAsync();
```

`UseKnightBus()` takes an optional callback for host-level configuration:

```csharp
.UseKnightBus(config => config.ShutdownGracePeriod = TimeSpan.FromMinutes(2))
```

`IHostConfiguration` exposes three members: `ShutdownGracePeriod`, `Log` and `DependencyInjection`.
The last two are set up by the host itself from the container, so `ShutdownGracePeriod` is normally
the only one you touch.

!!! note "`UseKnightBus()` sets two host options"
    It calls `UseConsoleLifetime()` and sets `HostOptions.ShutdownTimeout` to
    `ShutdownGracePeriod + 10 seconds`, so the runtime does not abort while KnightBus is still
    draining. If you configure either of those yourself, apply `UseKnightBus()` afterwards.

## Registration extensions

Everything is configured through `IServiceCollection`.

| Call | Purpose |
| --- | --- |
| `UseTransport<T>()` | Starts listeners for a transport. Call once per transport. |
| `RegisterProcessors()` / `RegisterProcessor<T>()` | Discovers and registers message handlers. |
| `AddMiddleware<T>()` / `AddMiddleware(instance)` | Adds a [middleware](middleware.md) to every pipeline. |
| `AddPlugin<T>()` / `AddPlugin(instance)` | Adds a background component started with the host. |
| `UseSingletonLocks(manager)` | Supplies the distributed lock manager for [singleton processing](../features/singleton-processing.md). |
| `ThrottleHost(maxConcurrent)` | Caps concurrent message processing across the whole host. |
| `UseTcpAliveListener(port)` | Exposes a TCP liveness probe. |
| `UseDistributedTracing()` | Propagates a trace id across message hops. |

Transport-specific registrations (`UseServiceBus`, `UseBlobStorage`, `UsePostgres`, `UseRedis`,
`UseNats`) live on the [transport pages](../transports/index.md).

## Throttling the whole host

`MaxConcurrentCalls` limits one listener. `ThrottleHost` limits the process:

```csharp
services.ThrottleHost(maxConcurrent: 100);
```

This is a single semaphore shared by every pipeline, which makes it the right tool when the
constraint is a shared downstream resource — a database connection pool, an API rate limit — rather
than any one queue.

## Dependency injection

KnightBus uses `Microsoft.Extensions.DependencyInjection`. Each message is processed inside its own
DI scope, so scoped services behave like they do per web request, and are disposed when the message
finishes. Processors themselves are registered as scoped.

To supply your own scoping behaviour, register an `IMessageScopeProviderMiddleware` — see
[middleware pipeline](middleware.md#the-scope-provider).

## Logging

The host logs through `Microsoft.Extensions.Logging`, picking up whatever the generic host is
configured with. Inject `ILogger<T>` into processors and middleware as usual.

## Shutdown

KnightBus drains rather than waiting a fixed period. On `SIGTERM` or Ctrl-C:

1. Listeners stop fetching new messages immediately, and any
   [stoppable plugins](#plugins) are told to stop accepting work.
2. The host waits for in-flight messages to finish, polling every 100 ms, up to
   `ShutdownGracePeriod` (default **30 seconds**). If everything finishes in 200 ms, shutdown takes
   200 ms.
3. If messages are still running when the grace period expires, the host logs a warning and proceeds.
   Nothing is aborted — those messages simply never complete, and the transport redelivers them.
4. Only then are singleton locks released and stoppable plugins awaited, with a budget of whatever
   remains of the grace period, floored at 5 seconds.

Holding singleton locks until *after* the drain is deliberate: the next instance cannot pick up the
queue while this one is still finishing, so there is no overlap during a rolling deploy.

Set the grace period to comfortably exceed your longest expected message:

```csharp
.UseKnightBus(config => config.ShutdownGracePeriod = TimeSpan.FromMinutes(5))
```

In Kubernetes, make sure `terminationGracePeriodSeconds` exceeds
`ShutdownGracePeriod + 10s`, or the pod is killed mid-drain.

## Plugins

A plugin is a background component started with the host, after the listeners.

```csharp
public interface IPlugin
{
    Task StartAsync(CancellationToken cancellationToken);
}
```

Implement `IStoppablePlugin` when the component needs to shut down cleanly — its `StopAsync` is
called at the very beginning of shutdown, and awaited near the end, so it can stop accepting work
while letting in-flight work finish. A plugin that throws during shutdown is logged and does not
fail the shutdown.

```csharp
services.AddPlugin<MyBackgroundPlugin>();
```

Both the [cron scheduler](../features/scheduling.md) and the TCP liveness listener are plugins.

## Liveness probes

`UseTcpAliveListener` opens a TCP port that answers with the current UTC timestamp and closes:

```csharp
services.UseTcpAliveListener(13000);
```

The port is bound synchronously during startup, so a port clash fails the host rather than leaving
nothing listening. It stops answering as soon as shutdown begins, before the drain — which is what
you want, since it takes the instance out of rotation while it finishes its in-flight messages.

```yaml
livenessProbe:
  tcpSocket:
    port: 13000
  initialDelaySeconds: 15
  periodSeconds: 30
```

## Hosting targets

The host is a plain console application, so it runs anywhere .NET does. BookBeat runs KnightBus
hosts as Kubernetes pods, which is the best-supported target — the liveness listener and drain-aware
shutdown exist for exactly that.

## See also

- [Middleware pipeline](middleware.md) — the ordering rules for everything in the pipeline.
- [Monitoring](../monitoring.md) — traces, metrics and logs.
- [Message processors](processors.md) — per-listener concurrency settings.
