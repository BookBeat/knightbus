using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;
using NATS.Client;
using NATS.Client.JetStream;

namespace KnightBus.Nats
{
    public abstract class NatsChannelReceiverBase<T> : IChannelReceiver where T : class, IMessage
    {
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageProcessor _processor;
        private readonly SemaphoreSlim _maxConcurrent;
        private readonly ILogger _log;
        private IConnection _connection;

        public IProcessingSettings Settings { get; set; }

        protected NatsChannelReceiverBase(IProcessingSettings settings, IHostConfiguration hostConfiguration, IMessageSerializer serializer, IMessageProcessor processor)
        {
            _hostConfiguration = hostConfiguration;
            _serializer = serializer;
            _processor = processor;
            Settings = settings;
            _maxConcurrent = new SemaphoreSlim(Settings.MaxConcurrentCalls);
            _log = _hostConfiguration.Log;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _connection = _hostConfiguration.DependencyInjection.GetInstance<IConnection>();
            var subscription = Subscribe(_connection, cancellationToken);


            Task.Run(() => ListenForMessages(subscription, cancellationToken), cancellationToken);
            Task.Run(() =>
            {
                cancellationToken.WaitHandle.WaitOne();
                //Cancellation requested
                try
                {
                    _log.LogInformation($"Closing Nats queue consumer for {typeof(T).Name}");
                    subscription.Drain();
                    _connection.Close();

                }
                catch (Exception)
                {
                    //Swallow
                }
            }, CancellationToken.None);
            return Task.CompletedTask;
        }
        public abstract IJetStreamPullSubscription Subscribe(IConnection connection, CancellationToken cancellationToken);


        protected async Task ListenForMessages(IJetStreamPullSubscription subscription, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var messageExpiration = new CancellationTokenSource(Settings.MessageLockTimeout);
                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(messageExpiration.Token, cancellationToken);
                await _maxConcurrent.WaitAsync(linkedToken.Token);
                try
                {
                    var messages = subscription.Fetch(Settings.MaxConcurrentCalls, 1000);
                    foreach (var msg in messages)
                    {
#pragma warning disable CS4014
                        Task.Run(
                            () => ProcessMessage(msg, linkedToken.Token).ContinueWith(t =>
                            {
                                _maxConcurrent.Release();
                                messageExpiration.Dispose();
                                linkedToken.Dispose();

                            }, CancellationToken.None).ConfigureAwait(false), linkedToken.Token);
                    }
                }
                catch (Exception e)
                {
                    _log.LogError(e, "Error to read message from Nats");
                }
            }
        }

        private async Task ProcessMessage(Msg msg, CancellationToken token)
        {
            try
            {

                var stateHandler = new NatsMessageStateHandler<T>(msg, _serializer, Settings.DeadLetterDeliveryLimit, _hostConfiguration.DependencyInjection);
                await _processor.ProcessAsync(stateHandler, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _log.LogError(e, "Nats OnMessageHandler Error");
            }
        }
    }
}
