using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using NATS.Client;

namespace KnightBus.Nats
{
    public class NatsQueueChannelReceiver<T> : IChannelReceiver
        where T : class, ICommand
    {
        private readonly IProcessingSettings _settings;
        private readonly IMessageSerializer _serializer;
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageProcessor _processor;
        private readonly INatsBusConfiguration _configuration;
        public IProcessingSettings Settings { get; set; }
        private readonly ConnectionFactory _factory;
        private readonly ILog _log;
        private CancellationToken _cancellationToken;
        private IConnection _connection;

        public NatsQueueChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IHostConfiguration hostConfiguration, IMessageProcessor processor, INatsBusConfiguration configuration)
        {
            _settings = settings;
            _serializer = serializer;
            _hostConfiguration = hostConfiguration;
            _processor = processor;
            _configuration = configuration;
            _log = hostConfiguration.Log;
            _factory = new ConnectionFactory();
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;

            _connection = _factory.CreateConnection(_configuration.Options);
            
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var subscription = _connection.SubscribeAsync(queueName, queueName);
            subscription.PendingMessageLimit = _settings.MaxConcurrentCalls;
            subscription.MessageHandler += SubscriptionOnMessageHandler;
            subscription.Start();

#pragma warning disable 4014
            // ReSharper disable once MethodSupportsCancellation
            Task.Run(() =>
            {
                _cancellationToken.WaitHandle.WaitOne();
                //Cancellation requested
                try
                {
                    _log.Information($"Closing Nats queue consumer for {typeof(T).Name}");
                    subscription.Drain();
                    _connection.Close();
                }
                catch (Exception)
                {
                    //Swallow
                }
            });
            return Task.CompletedTask;
#pragma warning restore 4014
        }

        private void SubscriptionOnMessageHandler(object sender, MsgHandlerEventArgs args)
        {
            var messageExpiration = new CancellationTokenSource(_settings.MessageLockTimeout);
            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(messageExpiration.Token, _cancellationToken);
            var stateHandler = new NatsBusMessageStateHandler<T>(args, _serializer, _settings.DeadLetterDeliveryLimit, _hostConfiguration.DependencyInjection);
#pragma warning disable CS4014
            _processor.ProcessAsync(stateHandler, linkedToken.Token)
                .ContinueWith(task =>
                    {
                        messageExpiration.Dispose();
                        linkedToken.Dispose();
                    }
                )
                .ConfigureAwait(false);
#pragma warning restore CS4014
        }
    }
}