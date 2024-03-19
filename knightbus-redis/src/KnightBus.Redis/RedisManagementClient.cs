using System.Collections.Generic;
using System.Threading.Tasks;
using KnightBus.Messages;
using KnightBus.Redis.Messages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace KnightBus.Redis;

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
    private readonly ILogger _log;
    private readonly IDatabase _db;
    private readonly IMessageSerializer _serializer;

    public RedisManagementClient(IRedisConfiguration configuration, ILogger log)
    {
        _log = log;
        _db = ConnectionMultiplexer.Connect(configuration.ConnectionString).GetDatabase(configuration.DatabaseId);
        _serializer = configuration.MessageSerializer;
    }

    public Task<long> GetMessageCount<T>() where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, _serializer, _log);
        return queueClient.GetMessageCount();
    }

    public Task<long> GetDeadletterMessageCount<T>() where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, _serializer, _log);
        return queueClient.GetDeadletterMessageCount();
    }

    public IAsyncEnumerable<RedisDeadletter<T>> PeekDeadlettersAsync<T>(int limit) where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, _serializer, _log);
        return queueClient.PeekDeadlettersAsync(limit);
    }

    public async Task RequeueDeadlettersAsync<T>(long count) where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, _serializer, _log);

        for (var i = 0; i < count; i++)
        {
            await queueClient.RequeueDeadletterAsync();
        }
    }

    public Task DeleteDeadletterAsync<T>(RedisDeadletter<T> deadletter) where T : class, IRedisMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, _serializer, _log);
        return queueClient.DeleteDeadletterAsync(deadletter);
    }
}
