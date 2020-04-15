using System.Threading.Tasks;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public interface IRedisManagementClient
    {
        Task<long> GetMessageCount<T>() where T : class, IRedisMessage;
        Task<long> GetDeadletterMessageCount<T>() where T : class, IRedisMessage;
        Task RequeueDeadLettersAsync<T>(long count) where T : class, IRedisMessage;
    }

    public class RedisManagementClient : IRedisManagementClient
    {
        private readonly IDatabase _db;

        public RedisManagementClient(RedisConfiguration configuration)
        {
            _db = ConnectionMultiplexer.Connect(configuration.ConnectionString).GetDatabase(configuration.DatabaseId);
        }

        public async Task<long> GetMessageCount<T>() where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db);
            return await queueClient.GetMessageCount();
        }

        public async Task<long> GetDeadletterMessageCount<T>() where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db);
            return await queueClient.GetDeadletterMessageCount();
        }

        public async Task RequeueDeadLettersAsync<T>(long count) where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db);

            for (var i = 0; i < count; i++)
            {
                await queueClient.RequeueDeadletterAsync();
            }
        }
    }
}
