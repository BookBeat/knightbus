using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    internal class LostMessageBackgroundService<T> where T : class, IRedisMessage
    {
        private readonly IDatabase _db;
        private readonly IMessageSerializer _serializer;
        private readonly TimeSpan _messageTimeout;
        private readonly string _queueName;


        internal LostMessageBackgroundService(IDatabase db, IMessageSerializer serializer, TimeSpan messageTimeout, string queueName)
        {
            _db = db;
            _serializer = serializer;
            _messageTimeout = messageTimeout;
            _queueName = queueName;
        }

        internal async Task Start(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                await DetectAndHandleLostMessages(_queueName).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
            }
        }


        private async Task DetectAndHandleLostMessages(string queueName)
        {
            const int take = 10;
            var start = -take;
            var stop = -1;
            while (true)
            {
                var listItems = await _db.ListRangeAsync(RedisQueueConventions.GetProcessingQueueName(queueName), start, stop).ConfigureAwait(false);
                if (!listItems.Any()) break;
                await Task.WhenAll(listItems.Select(redisValue => HandlePotentiallyLostMessage(queueName, redisValue))).ConfigureAwait(false);
                if (listItems.Length < 10) break;

                start -= take;
                stop -= take;
            }
        }

        private async Task<bool> HandlePotentiallyLostMessage(string queueName, RedisValue listItem)
        {
            var message = _serializer.Deserialize<T>(listItem);
            var hash = await _db.HashGetAllAsync(RedisQueueConventions.GetHashKey(queueName, message.Id)).ConfigureAwait(false);

            if (hash != null)
            {
                var hashEntry = hash.Single(x => x.Name == RedisHashKeys.LastProcessDate);
                var processTimeStamp = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(hashEntry.Value));
                if (processTimeStamp + _messageTimeout < DateTimeOffset.Now)
                {
                    //Message is lost or has exceeded maximum processing time
                    await RecoverLostMessageAsync(queueName, listItem, message.Id).ConfigureAwait(false);
                    return true;
                }
            }

            return false;
        }

        private async Task RecoverLostMessageAsync(string queueName, RedisValue redisMessage, string id)
        {
            var hashKey = RedisQueueConventions.GetHashKey(queueName, id);
#pragma warning disable 4014
            var tran =_db.CreateTransaction();
            tran.AddCondition(Condition.HashExists(hashKey, RedisHashKeys.LastProcessDate));
            tran.ListRemoveAsync(RedisQueueConventions.GetProcessingQueueName(queueName), redisMessage);
            tran.ListLeftPushAsync(queueName, redisMessage);
            tran.HashDeleteAsync(hashKey, RedisHashKeys.LastProcessDate);
#pragma warning restore 4014
            await tran.ExecuteAsync().ConfigureAwait(false);
        }
    }
}