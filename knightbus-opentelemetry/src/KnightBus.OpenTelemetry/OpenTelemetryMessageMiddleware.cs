using System.Diagnostics;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.OpenTelemetry;

/// <summary>
/// Middleware that creates an Activity (span) for each processed message.
/// Records message properties as tags and captures exceptions.
/// </summary>
public class OpenTelemetryMessageMiddleware : IMessageProcessorMiddleware
{
    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        var messageName = typeof(T).FullName!;

        // Start a new span for this message. Returns null if no listener is registered.
        using var activity = KnightBusDiagnostics.ActivitySource.StartActivity(
            messageName,
            ActivityKind.Consumer
        );

        if (activity is not null)
        {
            // Add semantic messaging attributes per OpenTelemetry conventions
            activity.SetTag("messaging.system", "knightbus");
            activity.SetTag("messaging.operation", "process");
            activity.SetTag("messaging.destination.name", messageName);

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
