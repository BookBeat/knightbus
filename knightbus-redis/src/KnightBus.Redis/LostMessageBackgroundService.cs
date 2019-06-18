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
        private readonly Random _random;


        internal LostMessageBackgroundService(IDatabase db, IMessageSerializer serializer, TimeSpan messageTimeout, string queueName)
        {
            _db = db;
            _serializer = serializer;
            _messageTimeout = messageTimeout;
            _queueName = queueName;
            _random = new Random(Guid.NewGuid().GetHashCode());
        }

        internal async Task Start(CancellationToken cancellationToken)
        {
            //Randomly delay so not all tasks fire at once
            await Task.Delay(TimeSpan.FromSeconds(_random.Next(1, 60)), cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                await DetectAndHandleLostMessages(_queueName).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken).ConfigureAwait(false);
            }
        }


        private async Task DetectAndHandleLostMessages(string queueName)
        {
            const int take = 100;
            var start = -take;
            var stop = -1;
            while (true)
            {
                var listItems = await _db.ListRangeAsync(RedisQueueConventions.GetProcessingQueueName(queueName), start, stop).ConfigureAwait(false);
                if (!listItems.Any()) break;
                foreach (var listItem in listItems)
                {
                    var (lost, message) = await HandlePotentiallyLostMessage(queueName, listItem).ConfigureAwait(false);
                    if (lost)
                    {
                        await RecoverLostMessageAsync(queueName, listItem, message.Id).ConfigureAwait(false);
                        await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
                    }
                }
                if (listItems.Length < take) break;

                start -= take;
                stop -= take;
            }
        }

        private async Task<(bool lost, T message)> HandlePotentiallyLostMessage(string queueName, RedisValue listItem)
        {
            var message = _serializer.Deserialize<T>(listItem);
            var hash = await _db.HashGetAsync(RedisQueueConventions.GetHashKey(queueName, message.Id), RedisHashKeys.LastProcessDate).ConfigureAwait(false);

            if (hash.IsNull)
            {
                var processTimeStamp = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(hash));
                if (processTimeStamp + _messageTimeout < DateTimeOffset.Now)
                {
                    //Message is lost or has exceeded maximum processing time
                    return (true, message);
                }
            }

            return (false, default);
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