using System.Diagnostics;
using KnightBus.Core.DistributedTracing;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace KnightBus.OpenTelemetry;

/// <summary>
/// Propagates W3C trace context (traceparent/tracestate) via message properties.
/// </summary>
public class OpenTelemetryDistributedTracingProvider : IDistributedTracingProvider
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    /// <summary>
    /// Called when receiving a message. Extracts trace context and links new activity to parent.
    /// </summary>
    public void SetProperties(IDictionary<string, string> properties)
    {
        var parentContext = Propagator.Extract(
            default,
            properties,
            (carrier, key) =>
            {
                if (carrier.TryGetValue(key, out var value))
                {
                    return [value];
                }

                return [];
            }
        );

        Baggage.Current = parentContext.Baggage;

        // Create a new activity linked to the parent trace context
        var activity = KnightBusDiagnostics.ActivitySource.StartActivity(
            "Process",
            ActivityKind.Consumer,
            parentContext.ActivityContext // <-- Links to parent trace
        );

        Activity.Current = activity;
    }

    /// <summary>
    /// Called when sending a message. Injects current trace context into message properties.
    /// </summary>
    public IDictionary<string, string> GetProperties()
    {
        var result = new Dictionary<string, string>();

        Propagator.Inject(
            new PropagationContext(Activity.Current?.Context ?? default, Baggage.Current),
            result,
            (carrier, key, value) => carrier[key] = value
        );

        return result;
    }
}
