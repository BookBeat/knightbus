using KnightBus.Core;
using KnightBus.Messages;
using KnightBus.PostgreSql.Messages;
using Npgsql;
using NpgsqlTypes;
using static KnightBus.PostgreSql.PostgresConstants;

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
        if (_queueName.Contains('-'))
            throw new ArgumentException("Postgres queue names can not contain '-'. Prefer using '_'");
    }

    public async Task<PostgresMessage<T>[]> GetMessagesAsync(int count, int visibilityTimeout)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
WITH cte AS
    (
        SELECT message_id
        FROM {SchemaName}.{QueuePrefix}_{_queueName}
        WHERE visibility_timeout <= clock_timestamp()
        ORDER BY message_id ASC
        LIMIT ($1)
        FOR UPDATE SKIP LOCKED
    )
UPDATE {SchemaName}.{QueuePrefix}_{_queueName} t
    SET
        visibility_timeout = clock_timestamp() + ($2),
        read_count = read_count + 1
        FROM cte
        WHERE t.message_id = cte.message_id
        RETURNING *;
");

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        command.Parameters.Add(new NpgsqlParameter<TimeSpan> { TypedValue = TimeSpan.FromSeconds(visibilityTimeout) });

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
DELETE FROM {SchemaName}.{QueuePrefix}_{_queueName};
");
        await command.ExecuteNonQueryAsync();
    }
    
    public async Task PurgeDeadLetterQueue()
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
DELETE FROM {SchemaName}.{QueuePrefix}_{_queueName};
");
        await command.ExecuteNonQueryAsync();
    }

    public async Task<PostgresMessage<T>[]> PeekDeadLettersAsync(int limit)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
SELECT message_id, enqueued_at, created_at, message, properties
FROM {SchemaName}.{DlQueuePrefix}_{_queueName}
ORDER BY message_id ASC
LIMIT ($1)
");

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = limit });
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
DELETE FROM {SchemaName}.{QueuePrefix}_{_queueName}
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
UPDATE {SchemaName}.{QueuePrefix}_{_queueName}
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
    DELETE FROM {SchemaName}.{QueuePrefix}_{_queueName}
    WHERE message_id = ($1)
    RETURNING message_id, enqueued_at, message, properties
)
INSERT INTO {SchemaName}.{DlQueuePrefix}_{_queueName} (message_id, enqueued_at, created_at, message, properties)
SELECT message_id, enqueued_at, now(), message, properties
FROM DeadLetter;
");
        command.Parameters.Add(new NpgsqlParameter<long> { Value = message.Id });
        await command.ExecuteNonQueryAsync();
    }

    public async Task InitQueue()
    {
        await using var connection = await _npgsqlDataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var createQueueCmd = new NpgsqlCommand(@$"
CREATE TABLE IF NOT EXISTS {SchemaName}.{QueuePrefix}_{_queueName} (
    message_id BIGINT PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    read_count INT DEFAULT 0 NOT NULL,
    enqueued_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    visibility_timeout TIMESTAMP WITH TIME ZONE NOT NULL,
    message JSONB,
    properties JSONB
);", connection);

        await using var createDlQueueCmd = new NpgsqlCommand(@$"
CREATE TABLE IF NOT EXISTS {SchemaName}.{DlQueuePrefix}_{_queueName} (
    message_id BIGINT PRIMARY KEY,
    enqueued_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    message JSONB,
    properties JSONB
);", connection);

        await using var createIndexCmd = new NpgsqlCommand(@$"
CREATE INDEX IF NOT EXISTS {SchemaName}_{QueuePrefix}_{_queueName}_visibility_timeout_idx
ON {SchemaName}.{QueuePrefix}_{_queueName} (visibility_timeout ASC);",
            connection);

        await using var createMetadataTableCmd = new NpgsqlCommand($@"
CREATE TABLE IF NOT EXISTS {SchemaName}.metadata (
    queue_name VARCHAR UNIQUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL
);", connection);

        await using var insertMetadataCmd = new NpgsqlCommand(@$"
INSERT INTO {SchemaName}.metadata (queue_name)
VALUES ('{QueuePrefix}_{_queueName}')
ON CONFLICT
DO NOTHING;",
            connection);

        await createQueueCmd.ExecuteNonQueryAsync();
        await createDlQueueCmd.ExecuteNonQueryAsync();
        await createIndexCmd.ExecuteNonQueryAsync();
        await createMetadataTableCmd.ExecuteNonQueryAsync();
        await insertMetadataCmd.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }
}
