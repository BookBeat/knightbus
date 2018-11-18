using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public class RedisBus
    {
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly RedisConfiguration _configuration;

        public RedisBus(RedisConfiguration configuration)
        {
            _multiplexer = ConnectionMultiplexer.Connect(configuration.ConnectionString);
            _configuration = configuration;
        }
        public Task SendAsync<T>(T message) where T : IRedisCommand
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var queueName = AutoMessageMapper.GetQueueName<T>();
            return db.ListRightPushAsync(queueName, _configuration.MessageSerializer.Serialize(message), When.Always, CommandFlags.FireAndForget)
                .ContinueWith(task => db.PublishAsync(queueName, 0, CommandFlags.FireAndForget), TaskContinuationOptions.OnlyOnRanToCompletion);
        }
        public Task SendAsync<T>(IEnumerable<T>  messages) where T : IRedisCommand
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var serialized = messages.Select(m =>  (RedisValue)_configuration.MessageSerializer.Serialize(m)).ToArray();
            return db.ListRightPushAsync(queueName, serialized, CommandFlags.FireAndForget)
                .ContinueWith(task => db.PublishAsync(queueName, 0, CommandFlags.FireAndForget), TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}