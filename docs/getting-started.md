# Getting started

This page takes you from an empty console application to a running message processor.

## Install

Every application needs the host package plus one transport. The transport is split into two
packages: the `*.Messages` package holds the marker interfaces your message contracts implement, and
the main package holds the client and the listener.

```bash
dotnet add package KnightBus.Host
dotnet add package KnightBus.Azure.ServiceBus
dotnet add package KnightBus.Azure.ServiceBus.Messages
```

!!! tip "Split your contracts"
    Reference only the `*.Messages` package from the assembly that defines your message contracts.
    It has almost no dependencies, so publishers and consumers can share contracts without dragging
    in the full transport.

All packages are published under the
[BookBeat profile on NuGet](https://www.nuget.org/profiles/BookBeat).

| Concern | Packages |
| --- | --- |
| Framework | `KnightBus.Core`, `KnightBus.Host`, `KnightBus.Messages` |
| Azure Service Bus | `KnightBus.Azure.ServiceBus`, `.Messages`, `.Management` |
| Azure Storage Queues | `KnightBus.Azure.Storage`, `.Messages`, `.Management` |
| PostgreSQL | `KnightBus.PostgreSql`, `.Messages`, `.Management`, `.Extensions.Azure` |
| Redis | `KnightBus.Redis`, `.Messages`, `.Management` |
| NATS | `KnightBus.Nats`, `.Messages` |
| Sagas (SQL Server store) | `KnightBus.SqlServer` |
| Cron scheduling | `KnightBus.Schedule` |
| Queue management | `KnightBus.Core.Management` |
| Monitoring | `KnightBus.OpenTelemetry`, `KnightBus.ApplicationInsights`, `KnightBus.NewRelic` |
| Serialization | `KnightBus.Newtonsoft` |

## The four pieces

A working message flow needs four types. Two describe the message, two describe the processing.

### 1. The message

The message implements a transport-specific marker interface. That interface — and nothing else —
decides which transport carries it.

```csharp
using KnightBus.Azure.ServiceBus.Messages;

public class SampleCommand : IServiceBusCommand
{
    public string Message { get; set; }
}
```

### 2. The mapping

Every message needs a mapping that names its queue or topic.

```csharp
using KnightBus.Messages;

public class SampleCommandMapping : IMessageMapping<SampleCommand>
{
    public string QueueName => "your-queue-name";
}
```

!!! warning "The mapping must live in the same assembly as the message"
    KnightBus discovers mappings by scanning the assembly that declares the message type. A mapping
    in a different assembly is never found, and you get a `MessageMappingMissingException`
    (`No queue name mapping exists for ...`) instead.

### 3. The processing settings

Settings control concurrency and retries for one listener. They are a separate type so several
processors can share them.

```csharp
using KnightBus.Core;

public class SampleSettings : IProcessingSettings
{
    public int MaxConcurrentCalls => 10;                              // messages in flight at once
    public int PrefetchCount => 50;                                   // messages fetched per batch
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5);    // how long processing may take
    public int DeadLetterDeliveryLimit => 3;                          // attempts before dead-lettering
}
```

### 4. The processor

The processor is the handler. Resolve dependencies through the constructor — it is created from the
DI container once per message.

```csharp
using KnightBus.Core;

public class SampleCommandProcessor : IProcessCommand<SampleCommand, SampleSettings>
{
    private readonly ILogger<SampleCommandProcessor> _logger;

    public SampleCommandProcessor(ILogger<SampleCommandProcessor> logger) => _logger = logger;

    public Task ProcessAsync(SampleCommand message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received: {Message}", message.Message);
        return Task.CompletedTask;
    }
}
```

## Start the host

`UseKnightBus()` registers KnightBus as a hosted service on a standard .NET generic host, so it
composes with everything else you already run.

```csharp
using KnightBus.Azure.ServiceBus;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;

var host = Host.CreateDefaultBuilder(args)
    .UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    })
    .ConfigureServices(services =>
    {
        services
            .UseServiceBus(config => config.ConnectionString = serviceBusConnection)
            .RegisterProcessors()
            .UseTransport<ServiceBusTransport>();
    })
    .UseKnightBus()
    .Build();

await host.RunAsync();
```

Three calls do the work:

| Call | Effect |
| --- | --- |
| `UseServiceBus(...)` | Registers the transport's configuration and its client (`IServiceBus`). |
| `RegisterProcessors()` | Scans the calling assembly for processors and registers them. Pass an `Assembly` to scan a different one. |
| `UseTransport<ServiceBusTransport>()` | Starts listeners for every registered processor whose message belongs to this transport. |

A message type with no matching registered transport fails at **startup**, not at send time, with
`No transport found for {type}, did you forget to register it?`.

### Running several transports at once

This is the point of KnightBus: register more than one transport and each message travels over the
one its interface names.

```csharp
services
    .UseServiceBus(config => config.ConnectionString = serviceBusConnection)
    .UseTransport<ServiceBusTransport>()
    .UsePostgres(config => config.ConnectionString = postgresConnection)
    .UseTransport<PostgresTransport>()
    .RegisterProcessors();
```

## Send messages

Each transport has its own client interface, resolved from DI.

```csharp
using var scope = host.Services.CreateScope();
var client = scope.ServiceProvider.GetRequiredService<IServiceBus>();

await client.SendAsync(new SampleCommand { Message = "Hello" });
```

!!! warning "Bus clients are scoped"
    Clients are registered with `AddScoped`. Resolving one straight from `host.Services` throws when
    `ValidateScopes` is on — create a scope first, or inject the client into another scoped service
    such as a `BackgroundService`'s dependency.

Method names differ slightly per transport (`PublishEventAsync` on Service Bus, `Publish` on NATS,
and so on). The [transport pages](transports/index.md) list the exact client surface for each.

## Where next

- **[Messages and mappings](concepts/messages.md)** — commands, events, requests and subscriptions.
- **[Message processors](concepts/processors.md)** — all four processor interfaces and what each
  setting does.
- **[Host and configuration](concepts/host.md)** — shutdown behaviour, plugins and throttling.
- **[Marker interfaces](reference/marker-interfaces.md)** — every interface that changes behaviour,
  on one page.

Complete runnable programs for each transport live in
[`knightbus/examples`](https://github.com/BookBeat/knightbus/tree/master/knightbus/examples).
