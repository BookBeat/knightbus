using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis;

internal class RedisCommandChannelReceiver<T> : RedisChannelReceiver<T>
    where T : class, IRedisCommand
{
    private readonly RedisConfiguration _configuration;

    public RedisCommandChannelReceiver(
        IConnectionMultiplexer connectionMultiplexer,
        IProcessingSettings settings,
        IMessageSerializer serializer,
        RedisConfiguration configuration,
        IHostConfiguration hostConfiguration,
        IMessageProcessor processor
    )
        : base(
            connectionMultiplexer,
            AutoMessageMapper.GetQueueName<T>(),
            settings,
            serializer,
            configuration,
            hostConfiguration,
            processor
        )
    {
        _configuration = configuration;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var db = ConnectionMultiplexer.GetDatabase(_configuration.DatabaseId);
        await db.SetAddAsync(
                RedisQueueConventions.QueueListKey,
                AutoMessageMapper.GetQueueName<T>()
            )
            .ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }
}
