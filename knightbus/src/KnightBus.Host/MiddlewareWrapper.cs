using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host
{
    internal class MiddlewareWrapper : IMessageProcessor
    {
        private readonly IMessageProcessorMiddleware _current;
        private readonly IMessageProcessor _next;

        public MiddlewareWrapper(IMessageProcessorMiddleware current, IMessageProcessor next)
        {
            _current = current;
            _next = next;
        }

        public Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, CancellationToken cancellationToken) where T : class, IMessage
        {
            return _current.ProcessAsync(messageStateHandler, _next, cancellationToken);
        }
    }
}