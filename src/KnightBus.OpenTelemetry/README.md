# KnightBus.OpenTelemetry

OpenTelemetry instrumentation for KnightBus message processing.

## Features

- Automatic span creation for each processed message
- W3C Trace Context propagation across message boundaries
- Exception recording with stack traces
- Message properties as span tags

## Installation

```bash
dotnet add package KnightBus.OpenTelemetry
```

## Usage

### 1. Register KnightBus OpenTelemetry

```csharp
services.UseOpenTelemetry();
```

### 2. Configure OpenTelemetry to capture KnightBus traces

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddKnightBusInstrumentation()
        .AddOtlpExporter());
```

### Zero-Code Instrumentation

If using the [OpenTelemetry .NET Automatic Instrumentation](https://opentelemetry.io/docs/zero-code/dotnet/), register the KnightBus source via environment variable:

```bash
export OTEL_DOTNET_AUTO_TRACES_ADDITIONAL_SOURCES="KnightBus"
```

## Span Attributes

Each message processing span includes:

| Attribute | Description |
|-----------|-------------|
| `messaging.system` | `knightbus` |
| `messaging.operation` | `process` |
| `messaging.destination.name` | Full type name of the message |
| `messaging.knightbus.*` | Message properties |

## Distributed Tracing

Trace context is automatically propagated via message properties using W3C Trace Context format (`traceparent`/`tracestate` headers). This enables end-to-end tracing across services communicating via KnightBus.
