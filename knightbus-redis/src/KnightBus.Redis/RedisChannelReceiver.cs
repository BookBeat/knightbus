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
    internal class RedisChannelReceiver<T> : IChannelReceiver
        where T : class, IRedisCommand
    {
        private readonly IProcessingSettings _settings;
        private readonly RedisConfiguration _configuration;
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageProcessor _processor;
        private IConnectionMultiplexer _multiplexer;
        private readonly string _queueName;
        private IDatabase db;
        private readonly SemaphoreSlim _maxConcurrent;
        private Task _runningTask;

        public RedisChannelReceiver(IProcessingSettings settings, RedisConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            _settings = settings;
            _configuration = configuration;
            _hostConfiguration = hostConfiguration;
            _processor = processor;
            _queueName = AutoMessageMapper.GetQueueName<T>();
            _maxConcurrent = new SemaphoreSlim(settings.MaxConcurrentCalls);
        }
        public async Task StartAsync()
        {
            _multiplexer = await ConnectionMultiplexer.ConnectAsync(_configuration.ConnectionString).ConfigureAwait(false);
            db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            //var sub = _multiplexer.GetSubscriber();
            //var kalle = await sub.SubscribeAsync(_queueName);
            //kalle.OnMessage(HandlerAsync);

            _runningTask = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    if (!await PumpAsync().ConfigureAwait(false))
                    {
                        await Task.Delay(500).ConfigureAwait(false);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }



        //private async Task HandlerAsync(ChannelMessage channel)
        //{
        //    var tasks = new List<Task>(_settings.PrefetchCount);

        //    for (int i = 0; i < _settings.PrefetchCount; i++)
        //    {
        //        tasks.Add(db.ListLeftPopAsync(_queueName).ContinueWith(ContinuationAction));
        //    }

        //    await Task.WhenAll(tasks);
        //}

        private async Task<bool> PumpAsync()
        {
            try
            {
                var prefetchCount = _settings.PrefetchCount > 0 ? _settings.PrefetchCount : 1;
                foreach (var redisMessage in await GetMessagesAsync(prefetchCount))
                {
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
            await  Task.WhenAll(Enumerable.Range(0, count).Select(i => Insert(i, array, cts)));
            return array;
        }

        private async Task Insert(int index, IList<RedisMessage<T>> array, CancellationTokenSource cancellationsSource)
        {
            if(cancellationsSource.IsCancellationRequested) return;
            var message = await GetMessageAsync().ConfigureAwait(false);
            if (message != null)
            {
                array[index] = message;
            }
            else
            {
                cancellationsSource.Cancel();
            }
        }


        private async Task<RedisMessage<T>> GetMessageAsync()
        {
            var listItem = await db.ListLeftPopAsync(_queueName).ConfigureAwait(false);
            if (listItem.IsNullOrEmpty) return null;
            var message = _configuration.MessageSerializer.Deserialize<T>(listItem);
            var hashKey = $"{_queueName}:{message.Id}";

            Task<HashEntry[]> hashGetTask = null;
            var tasks = new Task[]{
                    db.HashIncrementAsync(hashKey, RedisHashKeys.DeliveryCount, 1),
                    db.HashSetAsync(hashKey, RedisHashKeys.Message, listItem),
                    hashGetTask = db.HashGetAllAsync($"{_queueName}:{message.Id}")};
            await Task.WhenAll(tasks).ConfigureAwait(false);

            return new RedisMessage<T>(listItem, message, hashGetTask.Result, _queueName);
        }

        private async Task ProcessMessageAsync(RedisMessage<T> redisMessage, CancellationToken cancellationToken)
        {
            var stateHandler = new RedisMessageStateHandler<T>(_multiplexer, _configuration, redisMessage, _configuration.MessageSerializer, _settings.DeadLetterDeliveryLimit, _queueName);
            await _processor.ProcessAsync(stateHandler, cancellationToken).ConfigureAwait(false);
        }

        //private async Task ContinuationAction(Task<RedisValue> messageTask)
        //{
        //    try
        //    {
        //        var message = await messageTask;
        //        if (message.IsNullOrEmpty) return;

        //        var stateHandler = new RedisMessageStateHandler<T>(_multiplexer, _configuration, message, _configuration.MessageSerializer, _settings.DeadLetterDeliveryLimit, _queueName);
        //        await _processor.ProcessAsync(stateHandler, CancellationToken.None);
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e);
        //    }
        //}

        public IProcessingSettings Settings { get; set; }
    }
}
