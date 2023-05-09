using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisCommandChannelReceiver<T> : RedisChannelReceiver<T>
        where T : class, IRedisCommand
    {
        public RedisCommandChannelReceiver(IConnectionMultiplexer connectionMultiplexer, IProcessingSettings settings, IMessageSerializer serializer, RedisConfiguration configuration, IHostConfiguration hostConfiguration, IMessageProcessor processor)
            : base(connectionMultiplexer, AutoMessageMapper.GetQueueName<T>(), settings, serializer, configuration, hostConfiguration, processor)
        {
        }
    }
}
