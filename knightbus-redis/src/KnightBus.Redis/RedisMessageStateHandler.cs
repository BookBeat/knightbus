﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisMessageStateHandler<T> : IMessageStateHandler<T> where T : class, IMessage, IRedisMessage
    {
        private readonly IDatabase _db;
        private readonly RedisMessage<T> _redisMessage;
        private readonly string _queueName;


        public RedisMessageStateHandler(IConnectionMultiplexer connection, RedisConfiguration configuration, RedisMessage<T> redisMessage, int deadLetterDeliveryLimit, string queueName)
        {
            _db = connection.GetDatabase(configuration.DatabaseId);
            _redisMessage = redisMessage;
            DeadLetterDeliveryLimit = deadLetterDeliveryLimit;
            _queueName = queueName;
        }

        public int DeliveryCount => int.Parse(_redisMessage.HashEntries[RedisHashKeys.DeliveryCount]);
        public int DeadLetterDeliveryLimit { get; }

        public IDictionary<string, string> MessageProperties => _redisMessage.HashEntries;
        public Task CompleteAsync()
        {
            return _db.KeyDeleteAsync(_redisMessage.HashKey);
        }

        public Task AbandonByErrorAsync(Exception e)
        {
            return Task.WhenAll(
                _db.ListRightPushAsync(_queueName, _redisMessage.RedisValue),
                _db.PublishAsync(_queueName, 0, CommandFlags.FireAndForget));
        }

        public Task DeadLetterAsync(int deadLetterLimit)
        {
            return _db.ListRightPushAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName), _redisMessage.RedisValue);
        }

        public Task<T> GetMessageAsync()
        {
            return Task.FromResult(_redisMessage.Message);
        }
    }
}