using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public interface IRedisManagementClient
    {
        Task<int> GetQueueMessageCount<T>() where T : class, IRedisMessage;
        Task RequeueDeadLettersAsync<T>(int count) where T : class, IRedisMessage;
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

        public async Task<int> GetQueueMessageCount<T>() where T : class, IRedisMessage
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var queueClient = new RedisQueueClient<T>(db, _configuration.MessageSerializer, new NoLogging());
            return await queueClient.GetQueueMessageCount();
        }

        public async Task RequeueDeadLettersAsync<T>(int count) where T : class, IRedisMessage
        {
            var db = _multiplexer.GetDatabase(_configuration.DatabaseId);
            var queueClient = new RedisQueueClient<T>(db, _configuration.MessageSerializer, new NoLogging());

            for (var i = 0; i < count; i++)
            {
                await queueClient.RequeueDeadletterMessageAsync();
            }
        }
    }
}
