using System;
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
            var queueName = AutoMessageMapper.GetQueueName<T>();

            var log = pipelineInformation.HostConfiguration.Log;
            log.Debug("{ThreadCount} remaining threads that can process messages in {QueueName} in {Name}", _semaphoreQueue.CurrentCount, queueName, nameof(ThrottlingMiddleware));

            await _semaphoreQueue.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphoreQueue.Release();
            }
        }
    }
}