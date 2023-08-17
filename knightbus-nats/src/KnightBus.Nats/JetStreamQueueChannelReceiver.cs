using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Nats.Messages;
using Microsoft.Extensions.Logging;
using NATS.Client;
using NATS.Client.JetStream;

namespace KnightBus.Nats
{
    public class NatsQueueChannelReceiverBase<T> where T : class, IMessage
    {
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageProcessor _processor;
        private readonly SemaphoreSlim _maxConcurrent;
        private readonly ILogger _log;
        public IProcessingSettings Settings { get; set; }

        public NatsQueueChannelReceiverBase(IProcessingSettings settings, IHostConfiguration hostConfiguration, IMessageSerializer serializer, IMessageProcessor processor)
        {
            _hostConfiguration = hostConfiguration;
            _serializer = serializer;
            _processor = processor;
            Settings = settings;
            _maxConcurrent = new SemaphoreSlim(Settings.MaxConcurrentCalls);
            _log = _hostConfiguration.Log;
        }
        protected async Task ListenForMessages(ISyncSubscription subscription, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var messageExpiration = new CancellationTokenSource(Settings.MessageLockTimeout);
                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(messageExpiration.Token, cancellationToken);
                await _maxConcurrent.WaitAsync(linkedToken.Token);
                try
                {
                    var msg = subscription.NextMessage();
                    msg.Ack();

#pragma warning disable CS4014
                    Task.Run(
                        () => ProcessMessage(msg, linkedToken.Token).ContinueWith(t =>
                        {
                            _maxConcurrent.Release();
                            messageExpiration.Dispose();
                            linkedToken.Dispose();

                        }, CancellationToken.None).ConfigureAwait(false), linkedToken.Token);
                }
                catch (Exception e)
                {
                    _log.LogError(e, "Error to read message from Nats");
                }
#pragma warning restore CS4014
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
    public class JetStreamQueueChannelReceiver<T> : NatsQueueChannelReceiverBase<T>, IChannelReceiver
        where T : class, IMessage
    {
        private readonly IJetStreamConfiguration _configuration;
        private readonly IEventSubscription _subscription;
        private const string CommandQueueGroup = "qg";

        private readonly ConnectionFactory _factory;
        private readonly ILogger _log;
        private CancellationToken _cancellationToken;
        private IConnection _connection;

        public JetStreamQueueChannelReceiver(IProcessingSettings settings, IMessageSerializer serializer, IHostConfiguration hostConfiguration, IMessageProcessor processor, IJetStreamConfiguration configuration, IEventSubscription subscription)
            : base(settings, hostConfiguration, serializer, processor)
        {
            _configuration = configuration;
            _subscription = subscription;
            _log = hostConfiguration.Log;
            _factory = new ConnectionFactory();

        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _connection = _factory.CreateConnection(_configuration.Options);

            var queueName = AutoMessageMapper.GetQueueName<T>();
            ISyncSubscription subscription;


            if (typeof(INatsRequest).IsAssignableFrom(typeof(T)))
            {
                if (_subscription is null)
                    subscription = _connection.SubscribeSync(queueName, CommandQueueGroup);
                else
                    subscription = _connection.SubscribeSync(queueName, _subscription.Name);
            }
            else
            {
                var streamName = $"{queueName}-stream";
                var streamConfig = StreamConfiguration.Builder()
                    .WithName(streamName)
                    .WithSubjects(queueName)
                    .WithStorageType(StorageType.File)
                    .WithRetentionPolicy(RetentionPolicy.WorkQueue)
                    .Build();

                _connection.CreateJetStreamManagementContext(_configuration.JetStreamOptions)
                    .AddStream(streamConfig);

                var jetStream = _connection.CreateJetStreamContext(_configuration.JetStreamOptions);

                var durable = $"{streamName}_consumer";
                var consumerConfig = ConsumerConfiguration.Builder()
                    .WithDurable(durable)
                    .WithAckPolicy(AckPolicy.Explicit)
                    .WithFilterSubject(queueName)
                    .Build();

                var options = PushSubscribeOptions.Builder().WithConfiguration(consumerConfig).Build();

                if (_subscription is null)
                    subscription = jetStream.PushSubscribeSync(queueName, CommandQueueGroup, options);
                else
                    subscription = jetStream.PushSubscribeSync(queueName, _subscription.Name, options);
            }

#pragma warning disable CS4014
            Task.Run(() => ListenForMessages(subscription, _cancellationToken), cancellationToken);
#pragma warning restore CS4014

#pragma warning disable 4014
            // ReSharper disable once MethodSupportsCancellation
            Task.Run(() =>
            {
                _cancellationToken.WaitHandle.WaitOne();
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
            });
            return Task.CompletedTask;
#pragma warning restore 4014
        }




    }
}
