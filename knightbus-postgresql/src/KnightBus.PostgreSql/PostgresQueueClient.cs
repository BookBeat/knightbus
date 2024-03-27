using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Npgsql;
using NpgsqlTypes;

namespace KnightBus.PostgreSql;

public class PostgresQueueClient<T> where T : class, IPostgresCommand
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageSerializer _serializer;
    private readonly string _queueName;

    public PostgresQueueClient(NpgsqlDataSource npgsqlDataSource, IMessageSerializer serializer)
    {
        _npgsqlDataSource = npgsqlDataSource;
        _serializer = serializer;
        _queueName = AutoMessageMapper.GetQueueName<T>();
    }

    public async Task<PostgresMessage<T>[]> GetMessagesAsync(int count, int visibilityTimeout)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
WITH cte AS
    (
        SELECT message_id
        FROM knightbus.q_{_queueName}
        WHERE visibility_timeout <= clock_timestamp()
        ORDER BY message_id ASC
        LIMIT {count}
        FOR UPDATE SKIP LOCKED
    )
UPDATE knightbus.q_{_queueName} t
    SET
        visibility_timeout = clock_timestamp() + interval '{visibilityTimeout} seconds',
        read_count = read_count + 1
        FROM cte
        WHERE t.message_id = cte.message_id
        RETURNING *;
");

        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<PostgresMessage<T>>();
        while (await reader.ReadAsync())
        {
            var propertiesOrdinal = reader.GetOrdinal("properties");
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<T>
            {
                Id = reader.GetInt64(reader.GetOrdinal("message_id")),
                ReadCount = reader.GetInt32(reader.GetOrdinal("read_count")),
                Message = _serializer
                    .Deserialize<T>(reader.GetFieldValue<byte[]>(
                            reader.GetOrdinal("message"))
                        .AsMemory()),
                Properties = isPropertiesNull
                    ? new Dictionary<string, string>()
                    : _serializer
                        .Deserialize<Dictionary<string, string>>(
                            reader.GetFieldValue<byte[]>(propertiesOrdinal)
                                .AsMemory())
            };
            result.Add(postgresMessage);
        }

        return result.ToArray();
    }

    public async Task PurgeQueue()
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
DELETE FROM knightbus.q_{_queueName};
");
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task PurgeDeadLetterQueue()
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
DELETE FROM knightbus.dlq_{_queueName};
");
        await command.ExecuteNonQueryAsync();
    }

    public async Task<PostgresMessage<T>[]> PeekDeadLettersAsync(int limit)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
SELECT message_id, enqueued_at, created_at, message, properties
FROM knightbus.dlq_{_queueName}
ORDER BY message_id ASC
LIMIT {limit}
");

        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<PostgresMessage<T>>();
        while (await reader.ReadAsync())
        {
            var propertiesOrdinal = reader.GetOrdinal("properties");
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<T>
            {
                Id = reader.GetInt64(reader.GetOrdinal("message_id")),
                Message = _serializer
                    .Deserialize<T>(reader.GetFieldValue<byte[]>(
                            reader.GetOrdinal("message"))
                        .AsMemory()),
                Properties = isPropertiesNull
                    ? new Dictionary<string, string>()
                    : _serializer
                        .Deserialize<Dictionary<string, string>>(
                            reader.GetFieldValue<byte[]>(propertiesOrdinal)
                                .AsMemory())
            };
            result.Add(postgresMessage);
        }

        return result.ToArray();
    }

    public async Task CompleteAsync(PostgresMessage<T> message)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
DELETE FROM knightbus.q_{_queueName}
WHERE message_id = ($1);
");
        command.Parameters.Add(new NpgsqlParameter<long> { Value = message.Id });
        await command.ExecuteNonQueryAsync();
    }

    public async Task AbandonByErrorAsync(PostgresMessage<T> message, Exception exception)
    {
        var errorString = exception.ToString();
        message.Properties["error_message"] = errorString;

        await using var command = _npgsqlDataSource.CreateCommand(@$"
UPDATE knightbus.q_{_queueName}
SET properties = ($1), visibility_timeout = now()
WHERE message_id = ($2);
");
        command.Parameters.Add(new NpgsqlParameter
        {
            Value = _serializer.Serialize(message.Properties), NpgsqlDbType = NpgsqlDbType.Jsonb
        });
        command.Parameters.Add(new NpgsqlParameter<long> { Value = message.Id });
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeadLetterMessageAsync(PostgresMessage<T> message)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
WITH DeadLetter AS (
    DELETE FROM knightbus.q_{_queueName}
    WHERE message_id = ($1)
    RETURNING message_id, enqueued_at, message, properties
)
INSERT INTO knightbus.dlq_{_queueName} (message_id, enqueued_at, created_at, message, properties)
SELECT message_id, enqueued_at, now(), message, properties
FROM DeadLetter;
");
        command.Parameters.Add(new NpgsqlParameter<long> { Value = message.Id });
        await command.ExecuteNonQueryAsync();
    }
}
