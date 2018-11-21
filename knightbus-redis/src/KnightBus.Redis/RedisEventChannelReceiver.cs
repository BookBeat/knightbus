using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisEventChannelReceiver<T> : RedisChannelReceiver<T>
        where T : class, IRedisEvent
    {
        private readonly RedisConfiguration _configuration;
        private readonly IEventSubscription<T> _subscription;

        public RedisEventChannelReceiver(IConnectionMultiplexer connectionMultiplexer, IProcessingSettings settings, IEventSubscription<T> subscription, RedisConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
            :base(connectionMultiplexer, RedisQueueConventions.GetSubscriptionQueueName(AutoMessageMapper.GetQueueName<T>(), subscription.Name), settings, configuration, processor)
        {
            _subscription = subscription;
            _configuration = configuration;
        }

        public override async Task StartAsync()
        {
            var db = ConnectionMultiplexer.GetDatabase(_configuration.DatabaseId);
            await db.SetAddAsync(RedisQueueConventions.GetSubscriptionKey(AutoMessageMapper.GetQueueName<T>()), _subscription.Name);

            await base.StartAsync();
        }
    }
}