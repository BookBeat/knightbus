# KnightBus

*A fast, lightweight and extensible messaging framework for .NET that supports multiple active
message transports.*

When building BookBeat we discovered there is no silver bullet messaging technology — each one has
its own trade-offs in reliability, performance, latency, scalability, price and capability. KnightBus
exists so that the transport is a property of the *message*, not of the application. A single host
can listen to Azure Service Bus, PostgreSQL and Redis at the same time, and you pick the right one
per message type.

```csharp
// This message travels over Azure Service Bus...
public class OrderPlaced : IServiceBusEvent { }

// ...and this one over Redis, in the same host.
public class ThumbnailRequested : IRedisCommand { }
```

## Features

<div class="grid cards" markdown>

-   :material-transit-connection-variant: **Multiple transports**

    Azure Service Bus, Azure Storage Queues, PostgreSQL, Redis and NATS — all active
    simultaneously, chosen per message.

    [:octicons-arrow-right-24: Transports](transports/index.md)

-   :material-layers-triple: **Middleware**

    Everything in the processing pipeline is middleware, including KnightBus' own error handling
    and dead-lettering. Add your own.

    [:octicons-arrow-right-24: Middleware pipeline](concepts/middleware.md)

-   :material-paperclip: **Attachments**

    Attach arbitrarily large files to messages without hitting transport size limits. Transport
    independent.

    [:octicons-arrow-right-24: Attachments](features/attachments.md)

-   :material-numeric-1-box: **Singleton processing**

    Guarantee that only one message is processed at a time across every running instance, using a
    distributed lock.

    [:octicons-arrow-right-24: Singleton processing](features/singleton-processing.md)

-   :material-state-machine: **Sagas**

    Long-running, stateful workflows with optimistic concurrency and pluggable state stores.

    [:octicons-arrow-right-24: Sagas](features/sagas.md)

-   :material-speedometer: **Throttling and concurrency**

    Tune concurrency, prefetching and lock timeouts per message type, and throttle the host as a
    whole.

    [:octicons-arrow-right-24: Message processors](concepts/processors.md)

-   :material-clock-outline: **Scheduling**

    Cron-triggered recurring jobs, plus deferred message delivery on the transports that support
    it.

    [:octicons-arrow-right-24: Scheduling](features/scheduling.md)

-   :material-chart-timeline-variant: **Observability**

    OpenTelemetry, Application Insights and New Relic integrations, distributed trace propagation
    and a TCP liveness probe.

    [:octicons-arrow-right-24: Monitoring](monitoring.md)

</div>

## Where to start

- **[Getting started](getting-started.md)** — install the packages and get a message flowing.
- **[Core concepts](concepts/messages.md)** — messages, mappings, processors and the host.
- **[Marker interfaces reference](reference/marker-interfaces.md)** — the single page listing every
  interface that changes KnightBus' behaviour, and where to put it.

Runnable samples for every transport live in
[`samples`](https://github.com/BookBeat/knightbus/tree/master/samples) in the
repository. They are built by CI, so they always compile against the current API.

## Requirements

KnightBus targets `net9.0` and `net10.0`.

## Licence and credits

KnightBus is released under the [MIT licence](https://github.com/BookBeat/knightbus/blob/master/LICENSE),
copyright &copy; BookBeat.

Main author: **Niklas Arbin**, Systems Architect @ BookBeat.

With thanks to everyone who has contributed their time and intellect to KnightBus, including
Tobias Johansson, Albin Carnstam, Viktor Hartenberger, Magnus Baneryd, Olov Siktröm, Simon Aunér,
André Virdarson, Tobias Balzano, Peter Bergman and Björn Bylund — and the
[full list of contributors](https://github.com/BookBeat/knightbus/graphs/contributors) on GitHub.
