using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace KnightBus.Redis;

internal class RedisMessageStateHandler<T> : IMessageStateHandler<T> where T : class, IMessage, IRedisMessage
{
    private readonly RedisQueueClient<T> _queueClient;
    private readonly RedisMessage<T> _redisMessage;

    public RedisMessageStateHandler(IConnectionMultiplexer connection, RedisConfiguration configuration, RedisMessage<T> redisMessage, int deadLetterDeliveryLimit, IDependencyInjection messageScope, ILogger logger)
    {
        _redisMessage = redisMessage;
        _queueClient = new RedisQueueClient<T>(connection.GetDatabase(configuration.DatabaseId), configuration.MessageSerializer, logger);
        DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
        MessageScope = messageScope;
    }

    public int DeliveryCount => int.Parse(_redisMessage.HashEntries[RedisHashKeys.DeliveryCount]);
    public int DeadLetterDeliveryLimit { get; }

    public IDictionary<string, string> MessageProperties => _redisMessage.HashEntries;
    public Task CompleteAsync()
    {
        return _queueClient.CompleteMessageAsync(_redisMessage);
    }

    public Task ReplyAsync<TReply>(TReply reply)
    {
        throw new NotImplementedException();
    }

    public Task AbandonByErrorAsync(Exception e)
    {
        return _queueClient.AbandonMessageByErrorAsync(_redisMessage, e);
    }

    public Task DeadLetterAsync(int deadLetterLimit)
    {
        return _queueClient.DeadletterMessageAsync(_redisMessage, DeadLetterDeliveryLimit);
    }

    public T GetMessage()
    {
        return _redisMessage.Message;
    }

    public IDependencyInjection MessageScope { get; set; }
}
