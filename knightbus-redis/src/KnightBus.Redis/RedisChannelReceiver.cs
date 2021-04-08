using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal abstract class RedisChannelReceiver<T> : IChannelReceiver
        where T : class, IRedisMessage
    {
        private readonly RedisConfiguration _redisConfiguration;
        private readonly IHostConfiguration _hostConfiguration;
        private readonly SemaphoreQueue _maxConcurrent;
        private readonly IMessageProcessor _processor;
        private readonly string _queueName;
        protected readonly IConnectionMultiplexer ConnectionMultiplexer;
        private readonly IProcessingSettings _settings;
        private readonly IMessageSerializer _serializer;
        private CancellationTokenSource _pumpDelayCancellationTokenSource = new CancellationTokenSource();
        private Task _messagePumpTask;
        private Task _lostMessageTask;
        private LostMessageBackgroundService<T> _lostMessageService;
        private RedisQueueClient<T> _queueClient;

        protected RedisChannelReceiver(IConnectionMultiplexer connectionMultiplexer, string queueName, IProcessingSettings settings, IMessageSerializer serializer, RedisConfiguration redisConfiguration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            ConnectionMultiplexer = connectionMultiplexer;
            _settings = settings;
            _serializer = serializer;
            _redisConfiguration = redisConfiguration;
            _hostConfiguration = hostConfiguration;
            _processor = processor;
            _queueName = queueName;
            _maxConcurrent = new SemaphoreQueue(settings.MaxConcurrentCalls);
        }

        public virtual async Task StartAsync(CancellationToken cancellationToken)
        {
            _queueClient = new RedisQueueClient<T>(ConnectionMultiplexer.GetDatabase(_redisConfiguration.DatabaseId), _serializer, _hostConfiguration.Log);
            var sub = ConnectionMultiplexer.GetSubscriber();
            await sub.SubscribeAsync(_queueName, MessageSignalReceivedHandler);

            _messagePumpTask = Task.Factory.StartNew(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                    if (!await PumpAsync(cancellationToken).ConfigureAwait(false))
                        await Delay(_pumpDelayCancellationTokenSource.Token).ConfigureAwait(false);
            });
            _lostMessageService = new LostMessageBackgroundService<T>(ConnectionMultiplexer, _redisConfiguration.DatabaseId, _serializer, _hostConfiguration.Log, _settings.MessageLockTimeout, _queueName);
            _lostMessageTask = _lostMessageService.Start(cancellationToken);
        }

        public IProcessingSettings Settings { get; set; }

        private async Task Delay(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                //reset the delay
                _pumpDelayCancellationTokenSource = new CancellationTokenSource();
            }
        }

        private void MessageSignalReceivedHandler(RedisChannel channel, RedisValue redisValue)
        {
            //Cancel the pumps delay
            _pumpDelayCancellationTokenSource.Cancel();
        }

        private async Task<bool> PumpAsync(CancellationToken cancellationToken)
        {
            try
            {
                var prefetchCount = _settings.PrefetchCount > 0 ? _settings.PrefetchCount : 1;
                var messages = await _queueClient.GetMessagesAsync(prefetchCount).ConfigureAwait(false);
                if (messages.Length == 0) return false;

                foreach (var redisMessage in messages)
                {
                    if (redisMessage != null)
                    {
                        await _maxConcurrent.WaitAsync(cancellationToken).ConfigureAwait(false);
                        var timeoutToken = new CancellationTokenSource(_settings.MessageLockTimeout);
                        var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken.Token);
#pragma warning disable 4014
                        Task.Run(async () => await ProcessMessageAsync(redisMessage, linkedToken.Token).ConfigureAwait(false), timeoutToken.Token)
                            .ContinueWith(task2 =>
                            {
                                _maxConcurrent.Release();
                                timeoutToken.Dispose();
                                linkedToken.Dispose();
                            }).ConfigureAwait(false);
#pragma warning restore 4014
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                _hostConfiguration.Log.Error(e, "Redis message pump error");
                return false;
            }
        }

        private async Task ProcessMessageAsync(RedisMessage<T> redisMessage, CancellationToken cancellationToken)
        {
            var stateHandler = new RedisMessageStateHandler<T>(ConnectionMultiplexer, _redisConfiguration, redisMessage, _settings.DeadLetterDeliveryLimit, _hostConfiguration.DependencyInjection);
            await _processor.ProcessAsync(stateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}
