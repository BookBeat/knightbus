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

    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        IPipelineInformation pipelineInformation,
        IMessageProcessor next,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        T? message = null;
        try
        {
            message = messageStateHandler.GetMessage();
            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            //A handler stopped on purpose, by a shutdown or a hand-over of a singleton lock, is not
            //an error. The message is still abandoned so that it is delivered again
            if (e is OperationCanceledException && cancellationToken.IsCancellationRequested)
                _log.LogInformation(
                    "Processing of message {@" + typeof(T).Name + "} was cancelled",
                    message
                );
            else
                _log.LogError(e, "Error processing message {@" + typeof(T).Name + "}", message);
            try
            {
                await messageStateHandler.AbandonByErrorAsync(e).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _log.LogError(
                    exception,
                    "Failed to abandon message {@" + typeof(T).Name + "}",
                    message
                );
            }
        }
    }
}
