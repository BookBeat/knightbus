﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

[assembly: InternalsVisibleTo("KnightBus.Redis.Tests.Integration")]
[assembly: InternalsVisibleTo("KnightBus.Redis.Tests.Unit")]
namespace KnightBus.Redis
{
    internal class RedisQueueClient<T> where T : class, IRedisMessage
    {
        private readonly string _queueName = AutoMessageMapper.GetQueueName<T>();
        private readonly IDatabase _db;
        private readonly IMessageSerializer _serializer;
        private readonly ILog _log;

        internal RedisQueueClient(IDatabase db, IMessageSerializer serializer, ILog log = null)
        {
            _db = db;
            _serializer = serializer;
            _log = log ?? new NoLogging();
        }

        internal async Task<RedisMessage<T>[]> GetMessagesAsync(int count)
        {
            var queueMessageCount = await GetMessageCount(_queueName).ConfigureAwait(false);

            if (queueMessageCount < count)
                count = (int)queueMessageCount;

            var cts = new CancellationTokenSource();
            var messages = await Task.WhenAll(Enumerable.Range(0, count).Select(i => GetMessageAsync(cts)))
                .ContinueWith(t =>
                {
                    cts.Dispose();
                    return t.Result;
                })
                .ConfigureAwait(false);

            return messages;
        }

        private async Task<RedisMessage<T>> GetMessageAsync(CancellationTokenSource cancellationsSource)
        {
            if (cancellationsSource.IsCancellationRequested) return null;

            try
            {
                var listItem = await _db.ListRightPopLeftPushAsync(_queueName, RedisQueueConventions.GetProcessingQueueName(_queueName)).ConfigureAwait(false);
                if (listItem.IsNullOrEmpty)
                {
                    cancellationsSource.Cancel();
                    return null;
                }

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

        internal async Task RequeueDeadletterAsync()
        {
            var deadLetterQueueName = RedisQueueConventions.GetDeadLetterQueueName(_queueName);
            var deadLetterProcessingQueueName = RedisQueueConventions.GetProcessingQueueName(deadLetterQueueName);

            var listItem = await _db.ListRightPopLeftPushAsync(deadLetterQueueName, deadLetterProcessingQueueName).ConfigureAwait(false);
            if (listItem.IsNullOrEmpty) return;
            var message = _serializer.Deserialize<RedisListItem<T>>(listItem);
            var hashKey = RedisQueueConventions.GetMessageHashKey(_queueName, message.Id);
            var expirationKey = RedisQueueConventions.GetMessageExpirationKey(_queueName, message.Id);

            await _db.HashDeleteAsync(hashKey, new RedisValue[] { expirationKey, RedisHashKeys.DeliveryCount }).ConfigureAwait(false);
            await _db.ListRightPopLeftPushAsync(deadLetterProcessingQueueName, _queueName).ConfigureAwait(false);
            await _db.PublishAsync(_queueName, 0, CommandFlags.FireAndForget).ConfigureAwait(false);
        }

        internal Task<long> GetMessageCount()
        {
            return GetMessageCount(_queueName);
        }

        internal Task<long> GetDeadletterMessageCount()
        {
            return GetMessageCount(RedisQueueConventions.GetDeadLetterQueueName(_queueName));
        }

        private Task<long> GetMessageCount(string queueName)
        {
            return _db.ListLengthAsync(queueName);
        }

        internal Task CompleteMessageAsync(RedisMessage<T> message)
        {
            return Task.WhenAll(
                _db.KeyDeleteAsync(new RedisKey[] { message.HashKey, message.ExpirationKey }),
                _db.ListRemoveAsync(RedisQueueConventions.GetProcessingQueueName(_queueName), message.RedisValue, -1));
        }

        internal Task AbandonMessageByErrorAsync(RedisMessage<T> message, Exception e)
        {
            return Task.WhenAll(
                _db.HashSetAsync(message.HashKey, RedisHashKeys.Errors, $"{e.Message}\n{e.StackTrace}"),
                _db.ListLeftPushAsync(_queueName, message.RedisValue),
                _db.ListRemoveAsync(RedisQueueConventions.GetProcessingQueueName(_queueName), message.RedisValue, -1));
        }

        internal Task DeadletterMessageAsync(RedisMessage<T> message, int deadLetterLimit)
        {
            return Task.WhenAll(
                _db.HashSetAsync(message.HashKey, "MaxDeliveryCountExceeded", $"DeliveryCount exceeded limit of {deadLetterLimit}"),
                _db.ListLeftPushAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName), message.RedisValue),
                _db.ListRemoveAsync(RedisQueueConventions.GetProcessingQueueName(_queueName), message.RedisValue, -1));
        }

        internal async IAsyncEnumerable<RedisDeadletter<T>> PeekDeadlettersAsync(int limit)
        {
            if (limit >= 1) limit--; //0 is the first element of the list, thus 0 will return 1

            var values = await _db.ListRangeAsync(RedisQueueConventions.GetDeadLetterQueueName(_queueName), 0, limit).ConfigureAwait(false);

            foreach (var value in values)
            {
                var deadletter = new RedisDeadletter<T> {Message = _serializer.Deserialize<RedisListItem<T>>(value)};
                var hash = RedisQueueConventions.GetMessageHashKey(_queueName, deadletter.Message.Id);
                var hashes = await _db.HashGetAllAsync(hash).ConfigureAwait(false);
                deadletter.HashEntries = hashes.ToStringDictionary();
                yield return deadletter;
            }
        }

        internal Task DeleteDeadletterAsync(RedisDeadletter<T> deadletter)
        {
            var deadletterQueueName = RedisQueueConventions.GetDeadLetterQueueName(_queueName);
            var hash = RedisQueueConventions.GetMessageHashKey(_queueName, deadletter.Message.Id);
            var expirationKey = RedisQueueConventions.GetMessageExpirationKey(_queueName, deadletter.Message.Id);
            return Task.WhenAll(_db.KeyDeleteAsync(new RedisKey[] { hash, expirationKey }),
                _db.ListRemoveAsync(deadletterQueueName, _serializer.Serialize(deadletter.Message), -1));
        }
    }
}
