using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core
{
    /// <summary>
    /// Guarantees FIFO ordering of waiting tasks
    /// </summary>
    public class SemaphoreQueue
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<TaskCompletionSource<bool>> _queue = new ConcurrentQueue<TaskCompletionSource<bool>>();

        public int CurrentCount => _semaphore.CurrentCount;

        public SemaphoreQueue(int initialCount)
        {
            _semaphore = new SemaphoreSlim(initialCount);
        }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            _queue.Enqueue(tcs);
            _semaphore.WaitAsync(cancellationToken).ContinueWith(t =>
            {
                if (_queue.TryDequeue(out var popped))
                    popped.SetResult(true);
            }, cancellationToken);
            return tcs.Task;
        }

        public void Release()
        {
            _semaphore.Release();
        }
    }
}