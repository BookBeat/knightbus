using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host
{
    internal class MiddlewareWrapper : IMessageProcessor
    {
        private readonly IMessageProcessorMiddleware _current;
        private readonly IPipelineInformation _pipelineInformation;
        private readonly IMessageProcessor _next;

        public MiddlewareWrapper(IMessageProcessorMiddleware current, IPipelineInformation pipelineInformation, IMessageProcessor next)
        {
            _current = current;
            _pipelineInformation = pipelineInformation;
            _next = next;
        }

        public Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, CancellationToken cancellationToken) where T : class, IMessage
        {
            return _current.ProcessAsync(messageStateHandler, _pipelineInformation, _next, cancellationToken);
        }
    }
}