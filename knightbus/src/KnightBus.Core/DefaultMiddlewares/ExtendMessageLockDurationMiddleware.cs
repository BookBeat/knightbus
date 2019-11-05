using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Messages;

namespace KnightBus.Core.DefaultMiddlewares
{
    public class ExtendMessageLockDurationMiddleware : IMessageProcessorMiddleware
    {
        public async Task ProcessAsync<T>(IMessageStateHandler<T> messageStateHandler, IPipelineInformation pipelineInformation, IMessageProcessor next, CancellationToken cancellationToken) where T : class, IMessage
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (messageStateHandler is IMessageLockHandler<T> lockHandler && pipelineInformation.ProcessingSettings is IExtendMessageLockTimeout extendLock)
            {
#pragma warning disable 4014
                Task.Run(async () =>
                {
                        await RenewLock(extendLock.ExtensionInterval, extendLock.ExtensionDuration, cts.Token, lockHandler, pipelineInformation.HostConfiguration.Log).ConfigureAwait(false);
                    }, cts.Token);
#pragma warning restore 4014
            }

            try
            {
                await next.ProcessAsync(messageStateHandler, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                //Stop the lock renewal
                cts.Cancel();
            }
        }

        private async Task RenewLock<T>(TimeSpan interval, TimeSpan duration, CancellationToken cancellationToken, IMessageLockHandler<T> lockHandler, ILog log) where T : class, IMessage
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested) return;
                    await lockHandler.SetLockDuration(duration, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // this is anticipated when the timeout ends, if we are done with the message
                }
                catch (Exception e)
                {
                    log.Error(e, $"Failed to renew lock for {typeof(T).FullName}");
                }
            }
        }
    }
}
