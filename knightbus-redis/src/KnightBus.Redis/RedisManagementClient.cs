using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis
{
    public interface IRedisManagementClient
    {
        Task<long> GetMessageCount<T>() where T : class, IRedisMessage;
        Task<long> GetDeadletterMessageCount<T>() where T : class, IRedisMessage;
        IAsyncEnumerable<RedisDeadletter<T>> PeekDeadlettersAsync<T>(int limit) where T : class, IRedisMessage;
        Task RequeueDeadlettersAsync<T>(long count) where T : class, IRedisMessage;
        Task DeleteDeadletterAsync<T>(RedisDeadletter<T> deadletter) where T : class, IRedisMessage;
    }

    public class RedisManagementClient : IRedisManagementClient
    {
        private readonly IDatabase _db;
        private readonly IMessageSerializer _serializer;

        public RedisManagementClient(IRedisBusConfiguration configuration)
        {
            _db = ConnectionMultiplexer.Connect(configuration.ConnectionString).GetDatabase(configuration.DatabaseId);
            _serializer = configuration.MessageSerializer;
        }

        public Task<long> GetMessageCount<T>() where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db, _serializer);
            return queueClient.GetMessageCount();
        }

        public Task<long> GetDeadletterMessageCount<T>() where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db, _serializer);
            return queueClient.GetDeadletterMessageCount();
        }

        public IAsyncEnumerable<RedisDeadletter<T>> PeekDeadlettersAsync<T>(int limit) where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db, _serializer);
            return queueClient.PeekDeadlettersAsync(limit);
        }

        public async Task RequeueDeadlettersAsync<T>(long count) where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db, _serializer);

            for (var i = 0; i < count; i++)
            {
                await queueClient.RequeueDeadletterAsync();
            }
        }

        public Task DeleteDeadletterAsync<T>(RedisDeadletter<T> deadletter) where T : class, IRedisMessage
        {
            var queueClient = new RedisQueueClient<T>(_db, _serializer);
            return queueClient.DeleteDeadletterAsync(deadletter);
        }
    }
}
