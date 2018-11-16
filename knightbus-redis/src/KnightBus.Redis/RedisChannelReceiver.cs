using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisChannelReceiver<T> : IChannelReceiver
        where T : class, ICommand
    {
        private readonly IProcessingSettings _settings;
        private readonly RedisConfiguration _configuration;
        private readonly IHostConfiguration _hostConfiguration;
        private readonly IMessageProcessor _processor;
        private IConnectionMultiplexer _multiplexer;
        private readonly string _queueName;
        private IDatabase db;

        public RedisChannelReceiver(IProcessingSettings settings, RedisConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
        {
            _settings = settings;
            _configuration = configuration;
            _hostConfiguration = hostConfiguration;
            _processor = processor;
            _queueName = AutoMessageMapper.GetQueueName<T>();
        }
        public async Task StartAsync()
        {
            _multiplexer = await ConnectionMultiplexer.ConnectAsync(_configuration.ConnectionString);
            
            db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var sub = _multiplexer.GetSubscriber();
            var kalle = await sub.SubscribeAsync(_queueName);
            kalle.OnMessage(HandlerAsync);
        }

        

        private async Task HandlerAsync(ChannelMessage channel)
        {
            var tasks = new List<Task>(_settings.PrefetchCount);

            for (int i = 0; i < _settings.PrefetchCount; i++)
            {
                tasks.Add(db.ListLeftPopAsync(_queueName).ContinueWith(ContinuationAction));
            }

            await Task.WhenAll(tasks);
        }

        private async Task ContinuationAction(Task<RedisValue> messageTask)
        {
            try
            {
                var message = await messageTask;
                if (message.IsNullOrEmpty) return;

                var stateHandler = new RedisMessageStateHandler<T>(_multiplexer, _configuration, message, _configuration.MessageSerializer, _settings.DeadLetterDeliveryLimit, _queueName);
                await _processor.ProcessAsync(stateHandler, CancellationToken.None);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public IProcessingSettings Settings { get; set; }
    }

    internal class RedisMessageStateHandler<T> : IMessageStateHandler<T> where T : class, IMessage
    {
        private readonly IConnectionMultiplexer _connection;
        private readonly RedisConfiguration _configuration;
        private readonly RedisValue _redisMessage;
        private readonly IMessageSerializer _serializer;
        private readonly string _queueName;
        private readonly T _message;

        public RedisMessageStateHandler(IConnectionMultiplexer connection, RedisConfiguration configuration, RedisValue redisMessage, IMessageSerializer serializer, int deadLetterDeliveryLimit, string queueName)
        {
            _connection = connection;
            _configuration = configuration;
            _redisMessage = redisMessage;
            _serializer = serializer;
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            _queueName = queueName;
            _message = _serializer.Deserialize<T>(redisMessage);
        }
        public int DeliveryCount { get; }
        public int DeadLetterDeliveryLimit { get; }

        public IDictionary<string, string> MessageProperties { get; }
        public Task CompleteAsync()
        {
            //todo: Maybe use internal store?
            return Task.CompletedTask;
        }

        public async Task AbandonByErrorAsync(Exception e)
        {
            var db = _connection.GetDatabase(_configuration.DatabaseId);
            await db.ListRightPushAsync(_queueName, _redisMessage);
        }

        public async Task DeadLetterAsync(int deadLetterLimit)
        {
            var db = _connection.GetDatabase(_configuration.DatabaseId);
            await db.ListRightPushAsync($"{_queueName}:dl", _redisMessage);
        }

        public Task<T> GetMessageAsync()
        {
            return Task.FromResult(_message);
        }
    }

    internal class RedisMessage
    {

    }

    public class RedisConfiguration : ITransportConfiguration
    {
        public RedisConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }
        public string ConnectionString { get; }
        public IMessageSerializer MessageSerializer { get; set; } = new JsonMessageSerializer();
        public int DatabaseId { get; set; }
    }
}
