using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core.Sagas;
using KnightBus.Core.Sagas.Exceptions;
using KnightBus.Messages;
using StackExchange.Redis;

namespace KnightBus.Redis;

public class RedisSagaStore : ISagaStore
{
    internal const string DataField = "data";
    internal const string StampField = "stamp";

    private const long Ok = 1;
    private const long Missing = 0;
    private const long Conflict = -1;

    private static readonly RedisValue[] Fields = [DataField, StampField];

    // KEYS[1] saga, ARGV[1] data, ARGV[2] stamp, ARGV[3] ttl in milliseconds
    private const string CreateScript = $$"""
        local kind = redis.call('TYPE', KEYS[1])['ok']
        if kind == 'hash' then
          return -1
        end
        if kind ~= 'none' then
          return redis.error_reply('WRONGTYPE ' .. KEYS[1] .. ' holds a ' .. kind .. ', not a saga hash')
        end
        redis.call('HSET', KEYS[1], '{{DataField}}', ARGV[1], '{{StampField}}', ARGV[2])
        redis.call('PEXPIRE', KEYS[1], ARGV[3])
        return 1
        """;

    // KEYS[1] saga, ARGV[1] data, ARGV[2] expected stamp or '', ARGV[3] new stamp
    private const string UpdateScript = $$"""
        if redis.call('EXISTS', KEYS[1]) == 0 then
          return 0
        end
        local current = redis.call('HGET', KEYS[1], '{{StampField}}')
        if ARGV[2] ~= '' and current ~= ARGV[2] then
          return -1
        end
        redis.call('HSET', KEYS[1], '{{DataField}}', ARGV[1], '{{StampField}}', ARGV[3])
        return 1
        """;

    // KEYS[1] saga, ARGV[1] expected stamp or ''
    private const string CompleteScript = $$"""
        if redis.call('EXISTS', KEYS[1]) == 0 then
          return 0
        end
        local current = redis.call('HGET', KEYS[1], '{{StampField}}')
        if ARGV[1] ~= '' and current ~= ARGV[1] then
          return -1
        end
        redis.call('DEL', KEYS[1])
        return 1
        """;

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IRedisConfiguration _configuration;
    private readonly IMessageSerializer _serializer;

    public RedisSagaStore(
        IConnectionMultiplexer connectionMultiplexer,
        IRedisConfiguration configuration
    )
    {
        _connectionMultiplexer = connectionMultiplexer;
        _configuration = configuration;
        _serializer = _configuration.MessageSerializer;
    }

    public async Task<SagaData<T>> GetSaga<T>(string partitionKey, string id, CancellationToken ct)
    {
        var key = GetKey(partitionKey, id);
        ct.ThrowIfCancellationRequested();
        var values = await GetDatabase().HashGetAsync(key, Fields).ConfigureAwait(false);
        if (values[0].IsNull)
            throw new SagaNotFoundException(partitionKey, id);
        return new SagaData<T>
        {
            Data = _serializer.Deserialize<T>((ReadOnlyMemory<byte>)values[0]),
            ConcurrencyStamp = values[1],
        };
    }

    public async Task<SagaData<T>> Create<T>(
        string partitionKey,
        string id,
        T data,
        TimeSpan ttl,
        CancellationToken ct
    )
    {
        var key = GetKey(partitionKey, id);
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(ttl),
                ttl,
                "The saga time to live must be positive"
            );
        ct.ThrowIfCancellationRequested();
        var stamp = NewStamp();
        var result = await GetDatabase()
            .ScriptEvaluateAsync(
                CreateScript,
                [key],
                [_serializer.Serialize(data), stamp, ToMilliseconds(ttl)]
            )
            .ConfigureAwait(false);
        switch (ReadCode(result))
        {
            case Ok:
                return new SagaData<T> { Data = data, ConcurrencyStamp = stamp };
            case Conflict:
                throw new SagaAlreadyStartedException(partitionKey, id);
            default:
                throw new SagaStorageFailedException(partitionKey, id);
        }
    }

    public async Task Update<T>(
        string partitionKey,
        string id,
        SagaData<T> sagaData,
        CancellationToken ct
    )
    {
        var key = GetKey(partitionKey, id);
        ArgumentNullException.ThrowIfNull(sagaData);
        ct.ThrowIfCancellationRequested();
        var stamp = NewStamp();
        var result = await GetDatabase()
            .ScriptEvaluateAsync(
                UpdateScript,
                [key],
                [
                    _serializer.Serialize(sagaData.Data),
                    sagaData.ConcurrencyStamp ?? string.Empty,
                    stamp,
                ]
            )
            .ConfigureAwait(false);
        ThrowUnlessOk(result, partitionKey, id);
        sagaData.ConcurrencyStamp = stamp;
    }

    public async Task Complete<T>(
        string partitionKey,
        string id,
        SagaData<T> sagaData,
        CancellationToken ct
    )
    {
        var key = GetKey(partitionKey, id);
        ArgumentNullException.ThrowIfNull(sagaData);
        ct.ThrowIfCancellationRequested();
        var result = await GetDatabase()
            .ScriptEvaluateAsync(CompleteScript, [key], [sagaData.ConcurrencyStamp ?? string.Empty])
            .ConfigureAwait(false);
        ThrowUnlessOk(result, partitionKey, id);
    }

    public async Task Delete(string partitionKey, string id, CancellationToken ct)
    {
        var key = GetKey(partitionKey, id);
        ct.ThrowIfCancellationRequested();
        var deleted = await GetDatabase().KeyDeleteAsync(key).ConfigureAwait(false);
        if (!deleted)
            throw new SagaNotFoundException(partitionKey, id);
    }

    private IDatabase GetDatabase() =>
        _connectionMultiplexer.GetDatabase(_configuration.DatabaseId);

    private static RedisKey GetKey(string partitionKey, string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(partitionKey);
        ArgumentException.ThrowIfNullOrEmpty(id);
        return RedisQueueConventions.GetSagaKey(partitionKey, id);
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");

    private static long ToMilliseconds(TimeSpan ttl) => (long)Math.Ceiling(ttl.TotalMilliseconds);

    private static long ReadCode(RedisResult result) =>
        result.IsNull ? long.MinValue : (long)result;

    private static void ThrowUnlessOk(RedisResult result, string partitionKey, string id)
    {
        switch (ReadCode(result))
        {
            case Ok:
                return;
            case Missing:
                throw new SagaNotFoundException(partitionKey, id);
            case Conflict:
                throw new SagaDataConflictException(partitionKey, id);
            default:
                throw new SagaStorageFailedException(partitionKey, id);
        }
    }
}
