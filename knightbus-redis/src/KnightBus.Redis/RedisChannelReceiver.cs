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
        private readonly RedisConfiguration _configuration;
        private readonly SemaphoreSlim _maxConcurrent;
        private readonly IMessageProcessor _processor;
        private readonly string _queueName;
        protected readonly IConnectionMultiplexer ConnectionMultiplexer;
        private readonly IProcessingSettings _settings;
        private CancellationTokenSource _pumpDelayCancellationTokenSource = new CancellationTokenSource();
        private Task _runningTask;
        private IDatabase _db;

        public RedisChannelReceiver(IConnectionMultiplexer connectionMultiplexer, string queueName, IProcessingSettings settings, RedisConfiguration configuration, IMessageProcessor processor)
        {
            ConnectionMultiplexer = connectionMultiplexer;
            _settings = settings;
            _configuration = configuration;
            _processor = processor;
            _queueName = queueName;
            _maxConcurrent = new SemaphoreSlim(settings.MaxConcurrentCalls);
        }

        public virtual async Task StartAsync()
        {
            _db = ConnectionMultiplexer.GetDatabase(_configuration.DatabaseId);
            var sub = ConnectionMultiplexer.GetSubscriber();
            await sub.SubscribeAsync(_queueName, Handler);

            _runningTask = Task.Factory.StartNew(async () =>
            {
                while (true)
                    if (!await PumpAsync().ConfigureAwait(false))
                        await Delay(_pumpDelayCancellationTokenSource.Token).ConfigureAwait(false);
            }, TaskCreationOptions.LongRunning);
        }


        public IProcessingSettings Settings { get; set; }

        protected async Task Delay(CancellationToken cancellationToken)
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

        private void Handler(RedisChannel channel, RedisValue redisValue)
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
                        await _maxConcurrent.WaitAsync().ConfigureAwait(false);
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
                Console.WriteLine(e);
                throw;
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
            var listItem = await _db.ListLeftPopAsync(_queueName).ConfigureAwait(false);
            if (listItem.IsNullOrEmpty) return null;
            var message = _configuration.MessageSerializer.Deserialize<T>(listItem);
            var hashKey = RedisQueueConventions.GetHashKey(_queueName, message.Id);

            Task<HashEntry[]> hashGetTask = null;
            var tasks = new Task[]
            {
                _db.HashIncrementAsync(hashKey, RedisHashKeys.DeliveryCount, 1),
                _db.HashSetAsync(hashKey, RedisHashKeys.Message, listItem),
                hashGetTask = _db.HashGetAllAsync(hashKey)
            };
            await Task.WhenAll(tasks).ConfigureAwait(false);

            return new RedisMessage<T>(listItem, message, hashGetTask.Result, _queueName);
        }

        private async Task ProcessMessageAsync(RedisMessage<T> redisMessage, CancellationToken cancellationToken)
        {
            var stateHandler = new RedisMessageStateHandler<T>(ConnectionMultiplexer, _configuration, redisMessage, _settings.DeadLetterDeliveryLimit, _queueName);
            await _processor.ProcessAsync(stateHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}