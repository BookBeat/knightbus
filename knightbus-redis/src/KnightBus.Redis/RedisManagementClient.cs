using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public interface IRedisManagementClient
    {
        Task<int> GetQueueMessageCount(string queueName);
        Task RequeueDeadLettersAsync<T>(int count) where T : IRedisMessage;
    }

    public class RedisManagementClient : IRedisManagementClient
    {
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly RedisConfiguration _configuration;

        public RedisManagementClient(RedisConfiguration configuration)
        {
            _multiplexer = ConnectionMultiplexer.Connect(configuration.ConnectionString);
            _configuration = configuration;
        }

        public async Task<int> GetQueueMessageCount(string queueName)
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var messages = await db.ListRangeAsync(queueName);
            return messages.Length;
        }

        public async Task RequeueDeadLettersAsync<T>(int count) where T : IRedisMessage
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var queueName = AutoMessageMapper.GetQueueName<T>();
            var deadLetterQueueName = RedisQueueConventions.GetDeadLetterQueueName(queueName);
            var deadLetterProcessingQueueName = RedisQueueConventions.GetProcessingQueueName(deadLetterQueueName);

            for (var i = 0; i < count; i++)
            {
                var listItem = await db.ListRightPopLeftPushAsync(deadLetterQueueName, deadLetterProcessingQueueName).ConfigureAwait(false);
                if (listItem.IsNullOrEmpty) return;
                var message = _configuration.MessageSerializer.Deserialize<RedisListItem<T>>(listItem);
                var hashKey = RedisQueueConventions.GetMessageHashKey(queueName, message.Id);

                await db.KeyDeleteAsync(hashKey).ConfigureAwait(false);
                await db.ListRightPopLeftPushAsync(deadLetterProcessingQueueName, queueName).ConfigureAwait(false);
            }

            await db.PublishAsync(queueName, 0, CommandFlags.FireAndForget).ConfigureAwait(false);
        }
    }
}
