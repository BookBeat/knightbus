using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;

namespace KnightBus.Core.DefaultMiddlewares;

public class ErrorHandlingMiddleware : IMessageProcessorMiddleware
{
    private readonly ILogger _log;

    public ErrorHandlingMiddleware(ILogger log)
    {
        _log = log;
    }
    public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
    {
        T message = null;
        try
        {
            message = messageStateHandler.GetMessage();
            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _log.LogError(e, "Error processing message {@" + typeof(T).Name + "}", message);
            try
            {
                await messageStateHandler.AbandonByErrorAsync(e).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _log.LogError(exception, "Failed to abandon message {@" + typeof(T).Name + "}", message);
            }
        }
    }
}
