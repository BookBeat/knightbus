using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis;

internal abstract class RedisChannelReceiver<T> : IChannelReceiver
    where T : class, IMessage
{
    private readonly RedisConfiguration _redisConfiguration;
    private readonly IHostConfiguration _hostConfiguration;
    private readonly IMessageProcessor _processor;
    protected readonly IConnectionMultiplexer ConnectionMultiplexer;
    private readonly string _queueName;
    private readonly IMessageSerializer _serializer;

    protected RedisChannelReceiver(
        IConnectionMultiplexer connectionMultiplexer,
        string queueName,
        IProcessingSettings settings,
        IMessageSerializer serializer,
        RedisConfiguration redisConfiguration,
        IHostConfiguration hostConfiguration,
        IMessageProcessor processor
    )
    {
        ConnectionMultiplexer = connectionMultiplexer;
        _queueName = queueName;
        _serializer = serializer;
        Settings = settings;
        _redisConfiguration = redisConfiguration;
        _hostConfiguration = hostConfiguration;
        _processor = processor;
    }

    private async Task ProcessMessageAsync(
        RedisMessage<T> redisMessage,
        CancellationToken cancellationToken
    )
    {
        var stateHandler = new RedisMessageStateHandler<T>(
            ConnectionMultiplexer,
            _redisConfiguration,
            redisMessage,
            Settings.DeadLetterDeliveryLimit,
            _hostConfiguration.DependencyInjection,
            _hostConfiguration.Log
        );
        await _processor.ProcessAsync(stateHandler, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        var messagePump = new RedisMessagePump<T>(
            ConnectionMultiplexer,
            _queueName,
            _serializer,
            _redisConfiguration,
            Settings,
            _hostConfiguration.Log
        );
        await messagePump.StartAsync<T>(ProcessMessageAsync, cancellationToken);
    }

    public IProcessingSettings Settings { get; set; }
}
