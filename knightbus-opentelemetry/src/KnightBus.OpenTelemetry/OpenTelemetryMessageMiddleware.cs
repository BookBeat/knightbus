using System.Diagnostics;
using KnightBus.Core;
using KnightBus.Messages;
using OpenTelemetry.Context.Propagation;

namespace KnightBus.OpenTelemetry;

/// <summary>
/// Middleware that creates an Activity (span) for each processed message.
/// Records message properties as tags and captures exceptions.
/// </summary>
public class OpenTelemetryMessageMiddleware : IMessageProcessorMiddleware
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        var messageName = typeof(T).FullName!;

        // Extract parent context directly from message properties
        var parentContext = Propagator.Extract(
            default,
            messageStateHandler.MessageProperties,
            (carrier, key) =>
            {
                if (carrier.TryGetValue(key, out var value))
                {
                    return [value];
                }

                return [];
            }
        );

        // Start a new span for this message linked to the parent trace context
        // Returns null if no listener is registered
        using var activity = KnightBusDiagnostics.ActivitySource.StartActivity(
            messageName,
            ActivityKind.Consumer,
            parentContext.ActivityContext
        );

        if (activity is not null)
        {
            // Add semantic messaging attributes per OpenTelemetry conventions
            activity.SetTag("messaging.system", "knightbus");
            activity.SetTag("messaging.operation", "process");
            activity.SetTag("messaging.destination.name", messageName);

            // Add queue/subscription name
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var isEvent = typeof(IEvent).IsAssignableFrom(typeof(T));
            activity.SetTag(
                $"messaging.destination.{(isEvent ? "topic" : "queue")}.name",
                queueName
            );

            if (pipelineInformation.Subscription is not null)
            {
                activity.SetTag(
                    "messaging.destination.subscription.name",
                    pipelineInformation.Subscription.Name
                );
            }

            // Include message properties as custom tags
            foreach (var property in messageStateHandler.MessageProperties)
                activity.SetTag($"messaging.knightbus.{property.Key}", property.Value);
        }

        try
        {
            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception e)
        {
            // Record failure and exception details on the span
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            activity?.AddException(e);
            throw;
        }
    }
}
