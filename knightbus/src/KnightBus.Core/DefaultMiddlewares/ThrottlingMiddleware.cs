using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.DefaultMiddlewares
{
    public class ThrottlingMiddleware : IMessageProcessorMiddleware
    {
        private readonly SemaphoreQueue _semaphoreQueue;
        public int CurrentCount => _semaphoreQueue.CurrentCount;

        public ThrottlingMiddleware(int maxConcurrent)
        {
            _semaphoreQueue = new SemaphoreQueue(maxConcurrent);
        }
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            try
            {
                await _semaphoreQueue.WaitAsync(cancellationToken).ConfigureAwait(false);
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphoreQueue.Release();
            }
        }
    }
}