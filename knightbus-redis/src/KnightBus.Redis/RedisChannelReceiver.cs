using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
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
        private CancellationTokenSource _pumpDelayCancellationTokenSource = new CancellationTokenSource();
        private Task _messagePumpTask;
        private Task _lostMessageTask;
        private LostMessageBackgroundService<T> _lostMessageService;
        private RedisQueueClient<T> _queueClient;

        protected RedisChannelReceiver(IConnectionMultiplexer connectionMultiplexer, string queueName, IProcessingSettings settings, RedisConfiguration redisConfiguration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            ConnectionMultiplexer = connectionMultiplexer;
            _settings = settings;
            _redisConfiguration = redisConfiguration;
            _hostConfiguration = hostConfiguration;
            _processor = processor;
            _queueName = queueName;
            _maxConcurrent = new SemaphoreQueue(settings.MaxConcurrentCalls);
        }

        public virtual async Task StartAsync(CancellationToken cancellationToken)
        {
            _queueClient = new RedisQueueClient<T>(ConnectionMultiplexer.GetDatabase(_redisConfiguration.DatabaseId), _redisConfiguration.MessageSerializer, _hostConfiguration.Log);
            var sub = ConnectionMultiplexer.GetSubscriber();
            await sub.SubscribeAsync(_queueName, MessageSignalReceivedHandler);

            _messagePumpTask = Task.Factory.StartNew(async () =>
            {
                while (true)
                    if (!await PumpAsync(cancellationToken).ConfigureAwait(false))
                        await Delay(_pumpDelayCancellationTokenSource.Token).ConfigureAwait(false);
            }, cancellationToken);
            _lostMessageService = new LostMessageBackgroundService<T>(ConnectionMultiplexer, _redisConfiguration.DatabaseId, _redisConfiguration.MessageSerializer, _hostConfiguration.Log, _settings.MessageLockTimeout, _queueName);
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
                foreach (var redisMessage in await _queueClient.GetMessagesAsync(prefetchCount).ConfigureAwait(false))
                    if (redisMessage != null)
                    {
                        await _maxConcurrent.WaitAsync(cancellationToken).ConfigureAwait(false);
                        var cts = new CancellationTokenSource(_settings.MessageLockTimeout);
#pragma warning disable 4014
                        Task.Run(async () => await ProcessMessageAsync(redisMessage, cts.Token)
                            .ContinueWith(task2 => _maxConcurrent.Release(), cts.Token), cts.Token);
#pragma warning restore 4014
                    }
                    else
                    {
                        return false;
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
            var stateHandler = new RedisMessageStateHandler<T>(ConnectionMultiplexer, _redisConfiguration, redisMessage, _settings.DeadLetterDeliveryLimit);
            await _processor.ProcessAsync(stateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}
