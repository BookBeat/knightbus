﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnightBus.Messages;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace KnightBus.Redis;

public interface IRedisManagementClient
{
    Task<IEnumerable<string>> ListQueues();
    Task<IEnumerable<string>> ListTopics();
    Task<long> GetMessageCount<T>(string path)
        where T : class, IMessage;
    Task<long> GetDeadletterMessageCount<T>(string path)
        where T : class, IMessage;
    IAsyncEnumerable<RedisDeadletter<T>> PeekDeadlettersAsync<T>(string path, int limit)
        where T : class, IMessage;
    IAsyncEnumerable<RedisDeadletter<T>> ReadDeadlettersAsync<T>(string path, int limit)
        where T : class, IMessage;
    IAsyncEnumerable<RedisMessage<T>> PeekMessagesAsync<T>(string path, int limit)
        where T : class, IMessage;
    Task<int> RequeueDeadlettersAsync<T>(string path, int count)
        where T : class, IMessage;
    Task DeleteDeadletterAsync<T>(string path, RedisDeadletter<T> deadletter)
        where T : class, IMessage;
    Task DeleteQueueAsync<T>(string path)
        where T : class, IMessage;
}

public class RedisManagementClient : IRedisManagementClient
{
    private readonly ILogger _log;
    private readonly IDatabase _db;
    private readonly IMessageSerializer _serializer;

    public RedisManagementClient(
        IRedisConfiguration configuration,
        ILogger<RedisManagementClient> log
    )
    {
        _log = log;
        _db = ConnectionMultiplexer
            .Connect(configuration.ConnectionString)
            .GetDatabase(configuration.DatabaseId);
        _serializer = configuration.MessageSerializer;
    }

    public async Task<IEnumerable<string>> ListQueues()
    {
        var queues = await _db.SetMembersAsync(RedisQueueConventions.QueueListKey)
            .ConfigureAwait(false);
        return queues.Select(queue => queue.ToString());
    }

    public async Task<IEnumerable<string>> ListTopics()
    {
        var topics = await _db.SetMembersAsync(RedisQueueConventions.TopicListKey)
            .ConfigureAwait(false);
        return topics.Select(topic => topic.ToString());
    }

    public Task<long> GetMessageCount<T>(string path)
        where T : class, IMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.GetMessageCount();
    }

    public Task<long> GetDeadletterMessageCount<T>(string path)
        where T : class, IMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.GetDeadletterMessageCount();
    }

    public IAsyncEnumerable<RedisDeadletter<T>> PeekDeadlettersAsync<T>(string path, int limit)
        where T : class, IMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.PeekDeadlettersAsync(limit);
    }

    public IAsyncEnumerable<RedisDeadletter<T>> ReadDeadlettersAsync<T>(string path, int limit)
        where T : class, IMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.ReadDeadlettersAsync(limit);
    }

    public IAsyncEnumerable<RedisMessage<T>> PeekMessagesAsync<T>(string path, int limit)
        where T : class, IMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.PeekMessagesAsync(limit);
    }

    public async Task<int> RequeueDeadlettersAsync<T>(string path, int count)
        where T : class, IMessage
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

    public Task DeleteDeadletterAsync<T>(string path, RedisDeadletter<T> deadletter)
        where T : class, IMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.DeleteDeadletterAsync(deadletter);
    }

    public Task DeleteQueueAsync<T>(string path)
        where T : class, IMessage
    {
        var queueClient = new RedisQueueClient<T>(_db, path, _serializer, _log);
        return queueClient.DeleteQueueAsync();
    }
}
