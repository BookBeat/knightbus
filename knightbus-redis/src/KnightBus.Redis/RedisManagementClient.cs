using System.Threading.Tasks;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public interface IRedisManagementClient
    {
        Task<int> GetMessageCount<T>() where T : class, IRedisMessage;
        Task<int> GetDeadletterMessageCount<T>() where T : class, IRedisMessage;
        Task RequeueDeadLettersAsync<T>(int count) where T : class, IRedisMessage;
    }

    public class RedisManagementClient : IRedisManagementClient
    {
        private readonly IDatabase _db;

        public RedisManagementClient(RedisConfiguration configuration)
        {
            _db = ConnectionMultiplexer.Connect(configuration.ConnectionString).GetDatabase(configuration.DatabaseId);
        }

        public async Task<int> GetMessageCount<T>() where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db);
            return await queueClient.GetMessageCount();
        }

        public async Task<int> GetDeadletterMessageCount<T>() where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db);
            return await queueClient.GetDeadletterMessageCount();
        }

        public async Task RequeueDeadLettersAsync<T>(int count) where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db);

            for (var i = 0; i < count; i++)
            {
                await queueClient.RequeueDeadletterAsync();
            }
        }
    }
}
