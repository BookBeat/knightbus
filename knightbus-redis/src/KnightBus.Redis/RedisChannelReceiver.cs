using System;
using System.Collections.Generic;
using System.Linq;
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
        private IDatabase _db;
        private LostMessageBackgroundService<T> _lostMessageService;

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
            _db = ConnectionMultiplexer.GetDatabase(_redisConfiguration.DatabaseId);
            var sub = ConnectionMultiplexer.GetSubscriber();
            await sub.SubscribeAsync(_queueName, MessageSignalReceivedHandler);

            _messagePumpTask = Task.Factory.StartNew(async () =>
            {
                while (true)
                    if (!await PumpAsync().ConfigureAwait(false))
                        await Delay(_pumpDelayCancellationTokenSource.Token).ConfigureAwait(false);
            }, TaskCreationOptions.LongRunning);
            _lostMessageService = new LostMessageBackgroundService<T>(ConnectionMultiplexer, _redisConfiguration.DatabaseId, _redisConfiguration.MessageSerializer, _hostConfiguration.Log, _settings.MessageLockTimeout, _queueName);
            _lostMessageTask = _lostMessageService.Start(CancellationToken.None);

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

        private async Task<bool> PumpAsync()
        {
            try
            {
                var prefetchCount = _settings.PrefetchCount > 0 ? _settings.PrefetchCount : 1;
                foreach (var redisMessage in await GetMessagesAsync(prefetchCount).ConfigureAwait(false))
                    if (redisMessage != null)
                    {
                        await _maxConcurrent.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                        var cts = new CancellationTokenSource(_settings.MessageLockTimeout);
#pragma warning disable 4014
                        ProcessMessageAsync(redisMessage, cts.Token).ContinueWith(task2 => _maxConcurrent.Release());
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

        private async Task<RedisMessage<T>[]> GetMessagesAsync(int count)
        {
            var array = new RedisMessage<T>[count];
            var cts = new CancellationTokenSource();
            await Task.WhenAll(Enumerable.Range(0, count).Select(i => Insert(i, array, cts))).ConfigureAwait(false);
            return array;
        }

        private async Task Insert(int index, IList<RedisMessage<T>> array, CancellationTokenSource cancellationsSource)
        {
            if (cancellationsSource.IsCancellationRequested) return;
            var message = await GetMessageAsync().ConfigureAwait(false);
            if (message != null)
                array[index] = message;
            else
                cancellationsSource.Cancel();
        }


        private async Task<RedisMessage<T>> GetMessageAsync()
        {
            try
            {
                var listItem = await _db.ListRightPopLeftPushAsync(_queueName, RedisQueueConventions.GetProcessingQueueName(_queueName)).ConfigureAwait(false);
                if (listItem.IsNullOrEmpty) return null;
                var message = _redisConfiguration.MessageSerializer.Deserialize<T>(listItem);
                var hashKey = RedisQueueConventions.GetMessageHashKey(_queueName, message.Id);

                var tasks = new Task[]
                {
                    _db.StringSetAsync(RedisQueueConventions.GetMessageExpirationKey(_queueName, message.Id), DateTimeOffset.Now.ToUnixTimeMilliseconds()),
                    _db.HashIncrementAsync(hashKey, RedisHashKeys.DeliveryCount, 1),
                };
                await Task.WhenAll(tasks).ConfigureAwait(false);
                var hash = await _db.HashGetAllAsync(hashKey).ConfigureAwait(false);

                return new RedisMessage<T>(listItem, message, hash, _queueName);
            }
            catch (RedisTimeoutException e)
            {
                _hostConfiguration.Log.Error(e, "Error retrieving redis message");
                return null;
            }
            catch (RedisException e)
            {
                _hostConfiguration.Log.Error(e, "Error retrieving redis message");
                return null;
            }
        }

        private async Task ProcessMessageAsync(RedisMessage<T> redisMessage, CancellationToken cancellationToken)
        {
            var stateHandler = new RedisMessageStateHandler<T>(ConnectionMultiplexer, _redisConfiguration, redisMessage, _settings.DeadLetterDeliveryLimit, _queueName);
            await _processor.ProcessAsync(stateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}