using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;

namespace KnightBus.Host.Singleton;

/// <summary>
/// Counts the messages one singleton processor is handling, so its lock can be kept until they have
/// stopped. Wraps the whole pipeline, so error handling and abandoning are included.
/// </summary>
internal sealed class InFlightMessages(IMessageProcessor next) : IMessageProcessor
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private long _count;

    public long Count => Interlocked.Read(ref _count);

    public async Task ProcessAsync<T>(
        IMessageStateHandler<T> messageStateHandler,
        CancellationToken cancellationToken
    )
        where T : class, IMessage
    {
        Interlocked.Increment(ref _count);
        try
        {
            //A message that reaches the pipeline after the receiver was cancelled must not run: a
            //hand-over that saw no message in flight may already have released the lock
            if (cancellationToken.IsCancellationRequested)
            {
                await messageStateHandler
                    .AbandonByErrorAsync(new OperationCanceledException(cancellationToken))
                    .ConfigureAwait(false);
                return;
            }
            await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _count);
        }
    }

    /// <summary>
    /// Completes once no message is being handled
    /// </summary>
    public async Task WaitUntilIdleAsync(CancellationToken cancellationToken)
    {
        while (Count > 0)
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
    }
}
