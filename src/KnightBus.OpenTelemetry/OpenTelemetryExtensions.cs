using KnightBus.Core;
using KnightBus.Core.DistributedTracing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace KnightBus.OpenTelemetry;

public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing support to KnightBus.
    /// This registers the message processing middleware and distributed tracing provider.
    /// </summary>
    public static IServiceCollection UseOpenTelemetry(this IServiceCollection services)
    {
        services.UseDistributedTracing<OpenTelemetryDistributedTracingProvider>();
        services.AddMiddleware<OpenTelemetryMessageMiddleware>();
        return services;
    }

    /// <summary>
    /// Adds the KnightBus ActivitySource to the TracerProviderBuilder.
    /// Call this when configuring OpenTelemetry to capture KnightBus traces.
    /// </summary>
    public static TracerProviderBuilder AddKnightBusInstrumentation(
        this TracerProviderBuilder builder
    )
    {
        return builder.AddSource(KnightBusDiagnostics.Name);
    }
}
