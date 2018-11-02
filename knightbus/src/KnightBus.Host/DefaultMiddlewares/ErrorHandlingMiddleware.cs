using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host.DefaultMiddlewares
{
    internal class ErrorHandlingMiddleware : IMessageProcessorMiddleware
    {
        private readonly ILog _log;

        public ErrorHandlingMiddleware(ILog log)
        {
            _log = log;
        }
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            T message = null;
            try
            {
                message = await messageStateHandler.GetMessageAsync().ConfigureAwait(false);
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _log.Error(e, "Error processing message {@" + typeof(T).Name + "}", message);
                try
                {
                    await messageStateHandler.AbandonByErrorAsync(e).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _log.Error(exception, "Failed to abandon message {@" + typeof(T).Name + "}", message);
                }
            }
        }
    }
}