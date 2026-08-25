using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace KnightBus.ApplicationInsights;

public class ApplicationInsightsMessageMiddleware : IMessageProcessorMiddleware
{
    private readonly TelemetryClient _client;

    public ApplicationInsightsMessageMiddleware(TelemetryConfiguration configuration)
    {
        _client = new TelemetryClient(configuration);
    }

    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        var messageName = typeof(T).FullName;
        using (var operation = _client.StartOperation<RequestTelemetry>(messageName))
        {
            try
            {
                await next.ProcessAsync(messageStateHandler, cancellationToken)
                    .ConfigureAwait(false);

                // Add the message properties to the telemetry log
                foreach (var property in messageStateHandler.MessageProperties)
                    operation.Telemetry.Properties[property.Key] = property.Value;

                operation.Telemetry.Success = true;
            }
            catch (Exception e)
            {
                operation.Telemetry.Success = false;
                _client.TrackException(e);
                throw;
            }
        }
    }
}
