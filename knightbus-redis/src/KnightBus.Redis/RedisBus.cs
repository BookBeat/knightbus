﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.PreProcessors;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis;

/// <summary>
/// Client for sending messages over the <see cref="RedisTransport"/>
/// </summary>
public interface IRedisBus
{
    Task SendAsync<T>(T message)
        where T : IRedisCommand;
    Task SendAsync<T>(IEnumerable<T> messages)
        where T : IRedisCommand;
    Task PublishAsync<T>(T message)
        where T : IRedisEvent;
    Task PublishAsync<T>(IEnumerable<T> messages)
        where T : IRedisEvent;
}

public class RedisBus : IRedisBus
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IRedisConfiguration _configuration;
    private readonly ConcurrentDictionary<Type, IMessageSerializer> _serializers;
    private readonly IEnumerable<IMessagePreProcessor> _messagePreProcessors;

    public RedisBus(string connectionString, IEnumerable<IMessagePreProcessor> messagePreProcessors)
        : this(
            new RedisConfiguration(connectionString),
            ConnectionMultiplexer.Connect(connectionString),
            messagePreProcessors
        ) { }

    public RedisBus(
        IRedisConfiguration configuration,
        IConnectionMultiplexer multiplexer,
        IEnumerable<IMessagePreProcessor> messagePreProcessors
    )
    {
        _multiplexer = multiplexer;
        _messagePreProcessors = messagePreProcessors;
        _configuration = configuration;
        _serializers = new ConcurrentDictionary<Type, IMessageSerializer>();
    }

    public Task SendAsync<T>(T message)
        where T : IRedisCommand
    {
        var queueName = AutoMessageMapper.GetQueueName<T>();
        return SendAsync(message, queueName);
    }

    public Task SendAsync<T>(IEnumerable<T> messages)
        where T : IRedisCommand
    {
        var queueName = AutoMessageMapper.GetQueueName<T>();
        return SendAsync<T>(messages.ToList(), queueName);
    }

    private async Task SendAsync<T>(IList<T> messages, string queueName)
        where T : IMessage
    {
        var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
        var listItems = messages
            .Select(m => new RedisListItem<T>(Guid.NewGuid().ToString("N"), m))
            .ToList();
        var serialized = listItems
            .Select(m => (RedisValue)GetSerializer<T>().Serialize(m))
            .ToArray();
        foreach (var item in listItems)
        {
            await RunPreProcessors(item, db, queueName);
        }

        await Task.WhenAll(
            db.ListLeftPushAsync(queueName, serialized),
            db.PublishAsync(
                new RedisChannel(queueName, RedisChannel.PatternMode.Literal),
                0,
                CommandFlags.FireAndForget
            )
        );
    }

    private async Task SendAsync<T>(T message, string queueName)
        where T : IMessage
    {
        var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
        var redisListItem = new RedisListItem<T>(Guid.NewGuid().ToString("N"), message);
        await RunPreProcessors(redisListItem, db, queueName);
        await Task.WhenAll(
            db.ListLeftPushAsync(queueName, GetSerializer<T>().Serialize(redisListItem)),
            db.PublishAsync(
                new RedisChannel(queueName, RedisChannel.PatternMode.Literal),
                0,
                CommandFlags.FireAndForget
            )
        );
    }

    public async Task PublishAsync<T>(T message)
        where T : IRedisEvent
    {
        var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
        var queueName = AutoMessageMapper.GetQueueName<T>();
        var subscriptions = await db.SetMembersAsync(
                RedisQueueConventions.GetSubscriptionKey(queueName)
            )
            .ConfigureAwait(false);
        await Task.WhenAll(
                subscriptions.Select(sub =>
                    SendAsync(
                        message,
                        RedisQueueConventions.GetSubscriptionQueueName(queueName, sub)
                    )
                )
            )
            .ConfigureAwait(false);
    }

    public async Task PublishAsync<T>(IEnumerable<T> messages)
        where T : IRedisEvent
    {
        var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
        var queueName = AutoMessageMapper.GetQueueName<T>();
        var subscriptions = await db.SetMembersAsync(
                RedisQueueConventions.GetSubscriptionKey(queueName)
            )
            .ConfigureAwait(false);
        var messageList = messages.ToList();
        await Task.WhenAll(
                subscriptions.Select(sub =>
                    SendAsync(
                        messageList,
                        RedisQueueConventions.GetSubscriptionQueueName(queueName, sub)
                    )
                )
            )
            .ConfigureAwait(false);
    }

    private async Task RunPreProcessors<T>(RedisListItem<T> item, IDatabase db, string queueName)
        where T : IMessage
    {
        foreach (var preProcessor in _messagePreProcessors)
        {
            var properties = await preProcessor.PreProcess(item.Body, CancellationToken.None);
            foreach (var property in properties)
            {
                await db.HashSetAsync(
                    RedisQueueConventions.GetMessageHashKey(queueName, item.Id),
                    property.Key,
                    property.Value.ToString()
                );
            }
        }
    }

    private IMessageSerializer GetSerializer<T>()
        where T : IMessage
    {
        return _serializers.GetOrAdd(
            typeof(T),
            _ =>
            {
                var mapper = AutoMessageMapper.GetMapping<T>();
                if (mapper is ICustomMessageSerializer serializer)
                    return serializer.MessageSerializer;
                return _configuration.MessageSerializer;
            }
        );
    }
}
