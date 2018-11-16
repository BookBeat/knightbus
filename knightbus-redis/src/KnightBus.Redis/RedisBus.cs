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
            return db.ListRightPushAsync(queueName, _configuration.MessageSerializer.Serialize(message))
                .ContinueWith(task => db.PublishAsync(queueName, 0, CommandFlags.FireAndForget), TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}