using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace KnightBus.Redis;

internal class RedisMessagePump<T> : GenericMessagePump<RedisMessage<T>, IMessage> where T : class, IMessage
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly string _queueName;
    private readonly IMessageSerializer _serializer;
    private readonly RedisConfiguration _redisConfiguration;
    private RedisQueueClient<T> _queueClient;
    private LostMessageBackgroundService<T> _lostMessageService;
    private Task _lostMessageTask;

    public RedisMessagePump(IConnectionMultiplexer connectionMultiplexer, string queueName, IMessageSerializer serializer, RedisConfiguration redisConfiguration, IProcessingSettings settings, ILogger log) : base(settings, log)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _queueName = queueName;
        _serializer = serializer;
        _redisConfiguration = redisConfiguration;
    }

    public override async Task StartAsync<TMessage>(Func<RedisMessage<T>, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        _queueClient = new RedisQueueClient<T>(_connectionMultiplexer.GetDatabase(_redisConfiguration.DatabaseId), AutoMessageMapper.GetQueueName<T>(), _serializer, Log);
        var sub = _connectionMultiplexer.GetSubscriber();
        await sub.SubscribeAsync(new RedisChannel(_queueName, RedisChannel.PatternMode.Literal), MessageSignalReceivedHandler);
        await base.StartAsync<TMessage>(action, cancellationToken);
        _lostMessageService = new LostMessageBackgroundService<T>(_connectionMultiplexer, _redisConfiguration.DatabaseId, _serializer, Log, Settings.MessageLockTimeout, _queueName);
        _lostMessageTask = _lostMessageService.Start(cancellationToken);
    }

    private void MessageSignalReceivedHandler(RedisChannel channel, RedisValue redisValue)
    {
        CancelPollingDelay();
    }

    protected override async IAsyncEnumerable<RedisMessage<T>> GetMessagesAsync<TMessage>(int count, TimeSpan? lockDuration)
    {
        var messages = await _queueClient.GetMessagesAsync(count);
        foreach (var message in messages)
        {
            yield return message;
        }
    }

    protected override Task CreateChannel(Type messageType)
    {
        return Task.CompletedTask;
    }

    protected override bool ShouldCreateChannel(Exception e)
    {
        return false;
    }

    protected override Task CleanupResources()
    {
        return Task.CompletedTask;
    }

    protected override TimeSpan PollingDelay => TimeSpan.FromSeconds(5);
    protected override int MaxFetch => int.MaxValue;
}
