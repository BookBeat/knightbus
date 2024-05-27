using System.Runtime.CompilerServices;
using KnightBus.Messages;
using Npgsql;
using NpgsqlTypes;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public class PostgresBaseClient<T> where T : class, IMessage
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageSerializer _serializer;
    private readonly string _prefix;
    private readonly PostgresQueueName _queueName;
    
    public PostgresBaseClient(NpgsqlDataSource npgsqlDataSource, IMessageSerializer serializer, string prefix, PostgresQueueName queueName)
    {
        _npgsqlDataSource = npgsqlDataSource;
        _serializer = serializer;
        _prefix = prefix;
        _queueName = queueName;
    }

    public async IAsyncEnumerable<PostgresMessage<T>> GetMessagesAsync(int count, int visibilityTimeout, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(@$"
WITH cte AS
    (
        SELECT message_id
        FROM {SchemaName}.{_prefix}_{_queueName}
        WHERE visibility_timeout <= clock_timestamp()
        ORDER BY message_id ASC
        LIMIT ($1)
        FOR UPDATE SKIP LOCKED
    )
UPDATE {SchemaName}.{_prefix}_{_queueName} t
    SET
        visibility_timeout = clock_timestamp() + ($2),
        read_count = read_count + 1
        FROM cte
        WHERE t.message_id = cte.message_id
        RETURNING *;
", connection);

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        command.Parameters.Add(new NpgsqlParameter<TimeSpan> { TypedValue = TimeSpan.FromSeconds(visibilityTimeout) });

        await command.PrepareAsync(ct);

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        var propertiesOrdinal = reader.GetOrdinal("properties");
        var messageIdOrdinal = reader.GetOrdinal("message_id");
        var readCountOrdinal = reader.GetOrdinal("read_count");
        var messageOrdinal = reader.GetOrdinal("message");

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<T>
            {
                Id = reader.GetInt64(messageIdOrdinal),
                ReadCount = reader.GetInt32(readCountOrdinal),
                Message = _serializer
                    .Deserialize<T>(reader.GetFieldValue<byte[]>(messageOrdinal)
                        .AsMemory()),
                Properties = isPropertiesNull
                    ? new Dictionary<string, string>()
                    : _serializer
                        .Deserialize<Dictionary<string, string>>(
                            reader.GetFieldValue<byte[]>(propertiesOrdinal)
                                .AsMemory())
            };
            yield return postgresMessage;
        }
    }

    public async Task CompleteAsync(PostgresMessage<T> message)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
DELETE FROM {SchemaName}.{_prefix}_{_queueName}
WHERE message_id = ($1);
");
        command.Parameters.Add(new NpgsqlParameter<long> { Value = message.Id });
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task AbandonByErrorAsync(PostgresMessage<T> message, Exception exception)
    {
        var errorString = exception.ToString();
        message.Properties["error_message"] = errorString;

        await using var command = _npgsqlDataSource.CreateCommand(@$"
UPDATE {SchemaName}.{_prefix}_{_queueName}
SET properties = ($1), visibility_timeout = now()
WHERE message_id = ($2);
");
        command.Parameters.Add(new NpgsqlParameter
        {
            Value = _serializer.Serialize(message.Properties), NpgsqlDbType = NpgsqlDbType.Jsonb
        });
        command.Parameters.Add(new NpgsqlParameter<long> { Value = message.Id });
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task DeadLetterMessageAsync(PostgresMessage<T> message)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
WITH DeadLetter AS (
    DELETE FROM {SchemaName}.{_prefix}_{_queueName}
    WHERE message_id = ($1)
    RETURNING message_id, enqueued_at, message, properties
)
INSERT INTO {SchemaName}.{DlQueuePrefix}_{_queueName} (message_id, enqueued_at, created_at, message, properties)
SELECT message_id, enqueued_at, now(), message, properties
FROM DeadLetter;
");
        command.Parameters.Add(new NpgsqlParameter<long> { Value = message.Id });
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async IAsyncEnumerable<PostgresMessage<T>> PeekDeadLetterMessagesAsync(int count, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
SELECT message_id, enqueued_at, created_at, message, properties
FROM {SchemaName}.{DlQueuePrefix}_{_queueName}
ORDER BY message_id ASC
LIMIT ($1);
");

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        var propertiesOrdinal = reader.GetOrdinal("properties");
        var messageIdOrdinal = reader.GetOrdinal("message_id");
        var messageOrdinal = reader.GetOrdinal("message");

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<T>
            {
                Id = reader.GetInt64(messageIdOrdinal),
                Message = _serializer
                    .Deserialize<T>(reader.GetFieldValue<byte[]>(messageOrdinal)
                        .AsMemory()),
                Properties = isPropertiesNull
                    ? new Dictionary<string, string>()
                    : _serializer
                        .Deserialize<Dictionary<string, string>>(
                            reader.GetFieldValue<byte[]>(propertiesOrdinal)
                                .AsMemory())
            };
            yield return postgresMessage;
        }
    }
}
