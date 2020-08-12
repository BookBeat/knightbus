using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace KnightBus.Azure.ServiceBus
{
    internal class StoppableMessageReceiver : MessageReceiver
    {
        private CancellationTokenSource _receivePumpCancellationTokenSource;
        private MessageReceivePump _receivePump;

        public StoppableMessageReceiver(ServiceBusConnection serviceBusConnection, string entityPath, ReceiveMode receiveMode = ReceiveMode.PeekLock, RetryPolicy retryPolicy = null, int prefetchCount = 0) : base(serviceBusConnection, entityPath, receiveMode, retryPolicy, prefetchCount)
        {
        }

        public void StopPump()
        {
            _receivePumpCancellationTokenSource.Cancel();
        }

        public void RegisterStoppableMessageHandler(MessageHandlerOptions registerHandlerOptions, Func<Message, CancellationToken, Task> callback)
        {
            _receivePumpCancellationTokenSource = new CancellationTokenSource();
            _receivePump = new MessageReceivePump(this, registerHandlerOptions, callback, ServiceBusConnection.Endpoint, _receivePumpCancellationTokenSource.Token);
            _receivePump.StartPump();
        }


        sealed class MessageReceivePump
        {
            readonly Func<Message, CancellationToken, Task> _onMessageCallback;
            readonly string _endpoint;
            readonly MessageHandlerOptions _registerHandlerOptions;
            readonly IMessageReceiver _messageReceiver;
            readonly CancellationToken _pumpCancellationToken;
            readonly SemaphoreSlim _maxConcurrentCallsSemaphoreSlim;

            public MessageReceivePump(IMessageReceiver messageReceiver,
                MessageHandlerOptions registerHandlerOptions,
                Func<Message, CancellationToken, Task> callback,
                Uri endpoint,
                CancellationToken pumpCancellationToken)
            {
                _messageReceiver = messageReceiver ?? throw new ArgumentNullException(nameof(messageReceiver));
                _registerHandlerOptions = registerHandlerOptions;
                _onMessageCallback = callback;
                _endpoint = endpoint.Authority;
                _pumpCancellationToken = pumpCancellationToken;
                _maxConcurrentCallsSemaphoreSlim = new SemaphoreSlim(_registerHandlerOptions.MaxConcurrentCalls);
            }

            public void StartPump()
            {
                TaskExtensionHelper.Schedule(MessagePumpTaskAsync);
            }

            bool ShouldRenewLock()
            {
                return
                    _messageReceiver.ReceiveMode == ReceiveMode.PeekLock &&
                    _registerHandlerOptions.AutoRenewLock;
            }

            Task RaiseExceptionReceived(Exception e, string action)
            {
                var eventArgs = new ExceptionReceivedEventArgs(e, action, _endpoint, _messageReceiver.Path, _messageReceiver.ClientId);
                return _registerHandlerOptions.RaiseExceptionReceived(eventArgs);
            }

            async Task MessagePumpTaskAsync()
            {
                while (!_pumpCancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await _maxConcurrentCallsSemaphoreSlim.WaitAsync(_pumpCancellationToken).ConfigureAwait(false);

                        TaskExtensionHelper.Schedule(async () =>
                        {
                            Message message = null;
                            try
                            {
                                message = await _messageReceiver.ReceiveAsync(_registerHandlerOptions.ReceiveTimeOut).ConfigureAwait(false);
                                if (message != null)
                                {
                                    TaskExtensionHelper.Schedule(() =>
                                    {
                                        return MessageDispatchTask(message);
                                    });
                                }
                            }
                            catch (OperationCanceledException) when (_pumpCancellationToken.IsCancellationRequested)
                            {
                                // Ignore as we are stopping the pump
                            }
                            catch (ObjectDisposedException) when (_pumpCancellationToken.IsCancellationRequested)
                            {
                                // Ignore as we are stopping the pump
                            }
                            catch (Exception exception)
                            {
                                await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.Receive).ConfigureAwait(false);
                            }
                            finally
                            {
                                // Either an exception or for some reason message was null, release semaphore and retry.
                                if (message == null)
                                {
                                    _maxConcurrentCallsSemaphoreSlim.Release();
                                }
                            }
                        });
                    }
                    catch (OperationCanceledException) when (_pumpCancellationToken.IsCancellationRequested)
                    {
                        // Ignore as we are stopping the pump
                    }
                    catch (ObjectDisposedException) when (_pumpCancellationToken.IsCancellationRequested)
                    {
                        // Ignore as we are stopping the pump
                    }
                    catch (Exception exception)
                    {
                        await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.Receive).ConfigureAwait(false);
                    }
                }
            }


            async Task MessageDispatchTask(Message message)
            {
                CancellationTokenSource renewLockCancellationTokenSource = null;
                Timer autoRenewLockCancellationTimer = null;

                if (ShouldRenewLock())
                {
                    renewLockCancellationTokenSource = new CancellationTokenSource();
                    TaskExtensionHelper.Schedule(() => RenewMessageLockTask(message, renewLockCancellationTokenSource.Token));

                    // After a threshold time of renewal('AutoRenewTimeout'), create timer to cancel anymore renewals.
                    autoRenewLockCancellationTimer = new Timer(CancelAutoRenewLock, renewLockCancellationTokenSource, _registerHandlerOptions.MaxAutoRenewDuration, TimeSpan.FromMilliseconds(-1));
                }

                try
                {
                    await _onMessageCallback(message, _pumpCancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception)
                {

                    await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.UserCallback).ConfigureAwait(false);

                    // Nothing much to do if UserCallback throws, Abandon message and Release semaphore.
                    if (!(exception is MessageLockLostException))
                    {
                        await AbandonMessageIfNeededAsync(message).ConfigureAwait(false);
                    }

                    // AbandonMessageIfNeededAsync should take care of not throwing exception
                    _maxConcurrentCallsSemaphoreSlim.Release();

                    return;
                }
                finally
                {
                    renewLockCancellationTokenSource?.Cancel();
                    renewLockCancellationTokenSource?.Dispose();
                    autoRenewLockCancellationTimer?.Dispose();
                }

                // If we've made it this far, user callback completed fine. Complete message and Release semaphore.
                await CompleteMessageIfNeededAsync(message).ConfigureAwait(false);
                _maxConcurrentCallsSemaphoreSlim.Release();
            }

            void CancelAutoRenewLock(object state)
            {
                var renewLockCancellationTokenSource = (CancellationTokenSource)state;
                try
                {
                    renewLockCancellationTokenSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Ignore this race.
                }
            }

            async Task AbandonMessageIfNeededAsync(Message message)
            {
                try
                {
                    if (_messageReceiver.ReceiveMode == ReceiveMode.PeekLock)
                    {
                        await _messageReceiver.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.Abandon).ConfigureAwait(false);
                }
            }

            async Task CompleteMessageIfNeededAsync(Message message)
            {
                try
                {
                    if (_messageReceiver.ReceiveMode == ReceiveMode.PeekLock &&
                        _registerHandlerOptions.AutoComplete)
                    {
                        await _messageReceiver.CompleteAsync(new[] { message.SystemProperties.LockToken }).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.Complete).ConfigureAwait(false);
                }
            }

            async Task RenewMessageLockTask(Message message, CancellationToken renewLockCancellationToken)
            {
                while (!_pumpCancellationToken.IsCancellationRequested &&
                       !renewLockCancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var amount = MessagingUtilities.CalculateRenewAfterDuration(message.SystemProperties.LockedUntilUtc);

                        // We're awaiting the task created by 'ContinueWith' to avoid awaiting the Delay task which may be canceled
                        // by the renewLockCancellationToken. This way we prevent a TaskCanceledException.
                        var delayTask = await Task.Delay(amount, renewLockCancellationToken)
                            .ContinueWith(t => t, TaskContinuationOptions.ExecuteSynchronously)
                            .ConfigureAwait(false);
                        if (delayTask.IsCanceled)
                        {
                            break;
                        }

                        if (!_pumpCancellationToken.IsCancellationRequested &&
                            !renewLockCancellationToken.IsCancellationRequested)
                        {
                            await _messageReceiver.RenewLockAsync(message).ConfigureAwait(false);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (Exception exception)
                    {
                        // ObjectDisposedException should only happen here because the CancellationToken was disposed at which point
                        // this renew exception is not relevant anymore. Lets not bother user with this exception.
                        if (!(exception is ObjectDisposedException))
                        {
                            await RaiseExceptionReceived(exception, ExceptionReceivedEventArgsAction.RenewLock).ConfigureAwait(false);
                        }

                        if (!MessagingUtilities.ShouldRetry(exception))
                        {
                            break;
                        }
                    }
                }
            }


        }
        static class MessagingUtilities
        {
            public static TimeSpan CalculateRenewAfterDuration(DateTime lockedUntilUtc)
            {
                var remainingTime = lockedUntilUtc - DateTime.UtcNow;

                if (remainingTime < TimeSpan.FromMilliseconds(400))
                {
                    return TimeSpan.Zero;
                }

                var buffer = TimeSpan.FromTicks(Math.Min(remainingTime.Ticks / 2, Constants.MaximumRenewBufferDuration.Ticks));
                var renewAfter = remainingTime - buffer;

                return renewAfter;
            }

            public static bool ShouldRetry(Exception exception)
            {
                var serviceBusException = exception as ServiceBusException;
                return serviceBusException?.IsTransient == true;
            }
        }
        static class TaskExtensionHelper
        {
            public static void Schedule(Func<Task> func)
            {
                _ = ScheduleInternal(func);
            }

            static async Task ScheduleInternal(Func<Task> func)
            {
                try
                {
                    await func().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    //Ignore
                }
            }

        }

        static class Constants
        {


            public static readonly long DefaultLastPeekedSequenceNumber = 0;

            public static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromMinutes(1);

            public static readonly TimeSpan ClientPumpRenewLockTimeout = TimeSpan.FromMinutes(5);

            public static readonly TimeSpan MaximumRenewBufferDuration = TimeSpan.FromSeconds(10);

        }

        public sealed class MessageHandlerOptions
        {
            int maxConcurrentCalls;
            TimeSpan maxAutoRenewDuration;

            /// <summary>Initializes a new instance of the <see cref="MessageHandlerOptions" /> class.
            /// Default Values:
            ///     <see cref="MaxConcurrentCalls"/> = 1
            ///     <see cref="AutoComplete"/> = true
            ///     <see cref="ReceiveTimeOut"/> = 1 minute
            ///     <see cref="MaxAutoRenewDuration"/> = 5 minutes
            /// </summary>
            /// <param name="exceptionReceivedHandler">A <see cref="Func{T1, TResult}"/> that is invoked during exceptions.
            /// <see cref="ExceptionReceivedEventArgs"/> contains contextual information regarding the exception.</param>
            public MessageHandlerOptions(Func<ExceptionReceivedEventArgs, Task> exceptionReceivedHandler)
            {
                MaxConcurrentCalls = 1;
                AutoComplete = true;
                ReceiveTimeOut = Constants.DefaultOperationTimeout;
                MaxAutoRenewDuration = Constants.ClientPumpRenewLockTimeout;
                ExceptionReceivedHandler = exceptionReceivedHandler ?? throw new ArgumentNullException(nameof(exceptionReceivedHandler));
            }

            /// <summary>Occurs when an exception is received. Enables you to be notified of any errors encountered by the message pump.
            /// When errors are received calls will automatically be retried, so this is informational. </summary>
            public Func<ExceptionReceivedEventArgs, Task> ExceptionReceivedHandler { get; }

            /// <summary>Gets or sets the maximum number of concurrent calls to the callback the message pump should initiate.</summary>
            /// <value>The maximum number of concurrent calls to the callback.</value>
            public int MaxConcurrentCalls
            {
                get => maxConcurrentCalls;

                set
                {
                    if (value <= 0)
                    {
                        throw new ArgumentOutOfRangeException();
                    }

                    maxConcurrentCalls = value;
                }
            }

            /// <summary>Gets or sets a value that indicates whether the message-pump should call
            /// <see cref="QueueClient.CompleteAsync(string)" /> or
            /// <see cref="SubscriptionClient.CompleteAsync(string)" /> on messages after the callback has completed processing.</summary>
            /// <value>true to complete the message processing automatically on successful execution of the operation; otherwise, false.</value>
            public bool AutoComplete { get; set; }

            /// <summary>Gets or sets the maximum duration within which the lock will be renewed automatically. This
            /// value should be greater than the longest message lock duration; for example, the LockDuration Property. </summary>
            /// <value>The maximum duration during which locks are automatically renewed.</value>
            /// <remarks>The message renew can continue for sometime in the background
            /// after completion of message and result in a few false MessageLockLostExceptions temporarily.</remarks>
            public TimeSpan MaxAutoRenewDuration
            {
                get => maxAutoRenewDuration;

                set
                {
                    maxAutoRenewDuration = value;
                }
            }

            internal bool AutoRenewLock => MaxAutoRenewDuration > TimeSpan.Zero;

            internal TimeSpan ReceiveTimeOut { get; }

            internal async Task RaiseExceptionReceived(ExceptionReceivedEventArgs eventArgs)
            {
                try
                {
                    await ExceptionReceivedHandler(eventArgs).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    //Ignore
                }
            }
        }
    }
}