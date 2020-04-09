﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class RedisQueueClient<T> where T : class, IRedisMessage
    {
        private readonly string _queueName = AutoMessageMapper.GetQueueName<T>();
        private readonly IDatabase _db;
        private readonly IMessageSerializer _serializer;
        private readonly ILog _log;

        internal RedisQueueClient(IDatabase db, IMessageSerializer serializer, ILog log)
        {
            _db = db;
            _serializer = serializer;
            _log = log;
        }

        internal async Task<RedisMessage<T>[]> GetMessagesAsync(int count)
        {
            var array = new RedisMessage<T>[count];
            var cts = new CancellationTokenSource();
            await Task.WhenAll(Enumerable.Range(0, count).Select(i => Insert(i, array, cts))).ConfigureAwait(false);
            return array;
        }

        private async Task Insert(int index, IList<RedisMessage<T>> array, CancellationTokenSource cancellationsSource)
        {
            if (cancellationsSource.IsCancellationRequested) return;
            var message = await GetMessageAsync().ConfigureAwait(false);
            if (message != null)
                array[index] = message;
            else
                cancellationsSource.Cancel();
        }

        private async Task<RedisMessage<T>> GetMessageAsync()
        {
            try
            {
                var listItem = await _db.ListRightPopLeftPushAsync(_queueName, RedisQueueConventions.GetProcessingQueueName(_queueName)).ConfigureAwait(false);
                if (listItem.IsNullOrEmpty) return null;
                var message = _serializer.Deserialize<RedisListItem<T>>(listItem);
                var hashKey = RedisQueueConventions.GetMessageHashKey(_queueName, message.Id);

                var tasks = new Task[]
                {
                    _db.StringSetAsync(RedisQueueConventions.GetMessageExpirationKey(_queueName, message.Id), DateTimeOffset.Now.ToUnixTimeMilliseconds()),
                    _db.HashIncrementAsync(hashKey, RedisHashKeys.DeliveryCount)
                };
                await Task.WhenAll(tasks).ConfigureAwait(false);
                var hash = await _db.HashGetAllAsync(hashKey).ConfigureAwait(false);

                return new RedisMessage<T>(listItem, message.Id, message.Body, hash, _queueName);
            }
            catch (RedisTimeoutException e)
            {
                _log.Error(e, "Error retrieving redis message");
                return null;
            }
            catch (RedisException e)
            {
                _log.Error(e, "Error retrieving redis message");
                return null;
            }
        }

        internal async Task RequeueDeadletterMessageAsync()
        {
            var deadLetterQueueName = RedisQueueConventions.GetDeadLetterQueueName(_queueName);
            var deadLetterProcessingQueueName = RedisQueueConventions.GetProcessingQueueName(deadLetterQueueName);

            var listItem = await _db.ListRightPopLeftPushAsync(deadLetterQueueName, deadLetterProcessingQueueName).ConfigureAwait(false);
            if (listItem.IsNullOrEmpty) return;
            var message = _serializer.Deserialize<RedisListItem<T>>(listItem);
            var hashKey = RedisQueueConventions.GetMessageHashKey(_queueName, message.Id);
            var expirationKey = RedisQueueConventions.GetMessageExpirationKey(_queueName, message.Id);

            await _db.HashDeleteAsync(hashKey, new RedisValue[] { expirationKey, RedisHashKeys.DeliveryCount });
            await _db.ListRightPopLeftPushAsync(deadLetterProcessingQueueName, _queueName).ConfigureAwait(false);
            await _db.PublishAsync(_queueName, 0, CommandFlags.FireAndForget).ConfigureAwait(false);
        }

        internal async Task<int> GetQueueMessageCount()
        {
            var messages = await _db.ListRangeAsync(_queueName);
            return messages.Length;
        }
    }
}
