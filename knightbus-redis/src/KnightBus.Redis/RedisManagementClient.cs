using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace KnightBus.Redis;

public interface IRedisManagementClient
{
    Task<IEnumerable<string>> ListQueues();
    Task<IEnumerable<string>> ListTopics();
    Task<long> GetMessageCount<T>(string path) where T : class, IRedisMessage;
    Task<long> GetDeadletterMessageCount<T>(string path) where T : class, IRedisMessage;
    IAsyncEnumerable<RedisDeadletter<T>> PeekDeadlettersAsync<T>(string path, int limit) where T : class, IRedisMessage;
    IAsyncEnumerable<RedisMessage<T>> PeekMessagesAsync<T>(string path, int limit) where T : class, IRedisMessage;
    Task<int> RequeueDeadlettersAsync<T>(string path, int count) where T : class, IRedisMessage;
    Task DeleteDeadletterAsync<T>(string path, RedisDeadletter<T> deadletter) where T : class, IRedisMessage;
}

public class RedisManagementClient : IRedisManagementClient
{
    private readonly ILogger _log;
    private readonly IDatabase _db;
    private readonly IMessageSerializer _serializer;

    public RedisManagementClient(IRedisConfiguration configuration, ILogger log)
    {
        _log = log;
        _db = ConnectionMultiplexer.Connect(configuration.ConnectionString).GetDatabase(configuration.DatabaseId);
        _serializer = configuration.MessageSerializer;
    }

    public async Task<IEnumerable<string>> ListQueues()
    {
        var queues = await _db.SetMembersAsync(RedisQueueConventions.QueueListKey).ConfigureAwait(false);
        return queues.Select(queue => queue.ToString());
    }

    public async Task<IEnumerable<string>> ListTopics()
    {
        var topics = await _db.SetMembersAsync(RedisQueueConventions.TopicListKey).ConfigureAwait(false);
        return topics.Select(topic => topic.ToString());
    }

    public Task<long> GetMessageCount<T>(string path) where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.GetMessageCount();
    }

    public Task<long> GetDeadletterMessageCount<T>(string path) where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.GetDeadletterMessageCount();
    }

    public IAsyncEnumerable<RedisDeadletter<T>> PeekDeadlettersAsync<T>(string path, int limit) where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.PeekDeadlettersAsync(limit);
    }

    public IAsyncEnumerable<RedisMessage<T>> PeekMessagesAsync<T>(string path, int limit) where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.PeekMessagesAsync(limit);
    }

    public async Task<int> RequeueDeadlettersAsync<T>(string path, int count) where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);

        for (var i = 0; i < count; i++)
        {
            if (!await queueClient.RequeueDeadletterAsync())
            {
                return i;
            }
        }

        return count;
    }

    public Task DeleteDeadletterAsync<T>(string path, RedisDeadletter<T> deadletter) where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.DeleteDeadletterAsync(deadletter);
    }
}
