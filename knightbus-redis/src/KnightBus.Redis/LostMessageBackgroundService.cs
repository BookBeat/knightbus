using System;
using System.Diagnostics;
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
        private readonly ILog _log;
        private readonly TimeSpan _messageTimeout;
        private readonly TimeSpan _minimumInterval = TimeSpan.FromMinutes(1);
        private readonly string _queueName;
        private readonly Random _random;


        internal LostMessageBackgroundService(IConnectionMultiplexer multiplexer, int dbId, IMessageSerializer serializer, ILog log, TimeSpan messageTimeout, string queueName)
        {
            _db = multiplexer.GetDatabase(dbId);
            _serializer = serializer;
            _log = log;
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
                await Task.Delay(GetDelay(), cancellationToken).ConfigureAwait(false);
            }
        }

        private TimeSpan GetDelay()
        {
            return _minimumInterval > _messageTimeout ? _minimumInterval : _messageTimeout;
        }

        private async Task DetectAndHandleLostMessages(string queueName)
        {
            Debug.WriteLine($"Finding lost messages from {queueName}");
            const int take = 50;
            var start = -take;
            var stop = -1;
            try
            {
                while (true)
                {
                    var listItems = await _db.ListRangeAsync(RedisQueueConventions.GetProcessingQueueName(queueName), start, stop).ConfigureAwait(false);
                    if (!listItems.Any()) break;
                    Debug.WriteLine($"Found {listItems.Length} processing in {queueName}");

                    var checkedItems = await Task.WhenAll(listItems.Select(item => HandlePotentiallyLostMessage(queueName, item))).ConfigureAwait(false);

                    foreach (var (lost, message, listItem) in checkedItems.Where(item => item.lost))
                    {
                        if (await RecoverLostMessageAsync(queueName, listItem, message.Id).ConfigureAwait(false))
                        {
                            //Shift offset since we manipulated the end of the list and are using offsets
                            start += 1;
                            stop += 1;
                        }
                    }
                    if (listItems.Length < take) break;

                    start -= take;
                    stop -= take;
                }
            }
            catch (Exception e)
            {
                _log.Error(e, "Error in redis lost message service");
            }
        }

        private async Task<(bool lost, RedisListItem<T> message, RedisValue listItem)> HandlePotentiallyLostMessage(string queueName, byte[] listItem)
        {
            var message = _serializer.Deserialize<RedisListItem<T>>(listItem);
            var lastProcessedKey = RedisQueueConventions.GetMessageExpirationKey(queueName, message.Id);
            var hash = await _db.StringGetAsync(lastProcessedKey).ConfigureAwait(false);

            if (!hash.IsNull)
            {
                var processTimeStamp = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(hash));
                if (processTimeStamp + _messageTimeout < DateTimeOffset.Now)
                {
                    //Message is lost or has exceeded maximum processing time
                    return (true, message, listItem);
                }
            }
            else
            {
                await _db.StringSetAsync(lastProcessedKey, DateTimeOffset.Now.Add(-_messageTimeout).ToUnixTimeMilliseconds(), GetDelay()+GetDelay()).ConfigureAwait(false);
            }

            return (false, default, listItem);
        }

        private async Task<bool> RecoverLostMessageAsync(string queueName, RedisValue redisMessage, string id)
        {
            var lastProcessedKey = RedisQueueConventions.GetMessageExpirationKey(queueName, id);
#pragma warning disable 4014
            var tran = _db.CreateTransaction();
            tran.AddCondition(Condition.KeyExists(lastProcessedKey));
            tran.ListRemoveAsync(RedisQueueConventions.GetProcessingQueueName(queueName), redisMessage,-1);
            tran.ListLeftPushAsync(queueName, redisMessage);
            tran.KeyDeleteAsync(lastProcessedKey);

#pragma warning restore 4014
            var result = await tran.ExecuteAsync().ConfigureAwait(false);
            Debug.WriteLine($"Handled lost message {id}");
            return result;
        }
    }
}