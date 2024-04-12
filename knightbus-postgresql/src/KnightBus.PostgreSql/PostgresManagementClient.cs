using KnightBus.Messages;
using Npgsql;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public record PostgresQueueMetadata
{
    public string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int ActiveMessagesCount { get; set; }
    public int DeadLetterMessagesCount { get; set; }
}
public class PostgresManagementClient
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageSerializer _serializer;

    public PostgresManagementClient(NpgsqlDataSource npgsqlDataSource, IMessageSerializer serializer)
    {
        _npgsqlDataSource = npgsqlDataSource;
        _serializer = serializer;
    }

    public async Task<List<PostgresQueueMetadata>> ListQueues(CancellationToken ct)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
SELECT queue_name, created_at
FROM {SchemaName}.metadata;
");

        await using var reader = await command.ExecuteReaderAsync(ct);
        var queueMetas = new List<PostgresQueueMetadata>();
        while (await reader.ReadAsync(ct))
        {
            queueMetas.Add(new PostgresQueueMetadata
            {
                Name = reader.GetString(0),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(1)
            });
        }

        await using var conn = await _npgsqlDataSource.OpenConnectionAsync(ct);
        foreach (var queueMetadata in queueMetas)
        {
            await using var batch = new NpgsqlBatch(conn)
            {
                BatchCommands =
                {
                    new NpgsqlBatchCommand($@"
SELECT COUNT(*) FROM {SchemaName}.{QueuePrefix}_{queueMetadata.Name};"),
                    new NpgsqlBatchCommand($@"
SELECT COUNT(*) FROM {SchemaName}.{DlQueuePrefix}_{queueMetadata.Name};")

                }
            };

            await batch.PrepareAsync(ct);

            await using var batchReader = await batch.ExecuteReaderAsync(ct);
            await batchReader.ReadAsync(ct);
            queueMetadata.ActiveMessagesCount = batchReader.GetInt32(0);
            await batchReader.NextResultAsync(ct);
            await batchReader.ReadAsync(ct);
            queueMetadata.DeadLetterMessagesCount = batchReader.GetInt32(0);
            await batchReader.NextResultAsync(ct);
            await batchReader.ReadAsync(ct);
        }

        return queueMetas;
    }

    public async Task<PostgresQueueMetadata> GetQueue(PostgresQueueName queueName, CancellationToken ct)
    {
        await using var conn = await _npgsqlDataSource.OpenConnectionAsync(ct);
        await using var batch = new NpgsqlBatch(conn)
        {
            BatchCommands =
            {
                new NpgsqlBatchCommand($@"
SELECT COUNT(*) FROM {SchemaName}.{QueuePrefix}_{queueName};"),
                new NpgsqlBatchCommand($@"
SELECT COUNT(*) FROM {SchemaName}.{DlQueuePrefix}_{queueName};")

            }
        };

        var metadataCommand = new NpgsqlBatchCommand($@"
SELECT created_at FROM {SchemaName}.metadata
WHERE queue_name = ($1);");

        metadataCommand.Parameters.Add(new NpgsqlParameter<string> { TypedValue = queueName.Value });
        batch.BatchCommands.Add(metadataCommand);
        
        await batch.PrepareAsync(ct);

        var queueMeta = new PostgresQueueMetadata { Name = queueName.Value };
        await using var reader = await batch.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        queueMeta.ActiveMessagesCount = reader.GetInt32(0);
        await reader.NextResultAsync(ct);
        await reader.ReadAsync(ct);
        queueMeta.DeadLetterMessagesCount = reader.GetInt32(0);
        await reader.NextResultAsync(ct);
        await reader.ReadAsync(ct);
        queueMeta.CreatedAt = reader.GetFieldValue<DateTimeOffset>(0);
        return queueMeta;
    }

    public async Task<List<PostgresMessage<DictionaryMessage>>> PeekMessagesAsync(PostgresQueueName queueName, int count, CancellationToken ct)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
SELECT message_id, enqueued_at, read_count, message, properties
FROM {SchemaName}.{QueuePrefix}_{queueName}
ORDER BY message_id ASC
LIMIT ($1);
");

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });

        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<PostgresMessage<DictionaryMessage>>();
        while (await reader.ReadAsync(ct))
        {
            var propertiesOrdinal = reader.GetOrdinal("properties");
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<DictionaryMessage>
            {
                Id = reader.GetInt64(reader.GetOrdinal("message_id")),
                ReadCount = reader.GetInt32(reader.GetOrdinal("read_count")),
                Message = _serializer
                    .Deserialize<DictionaryMessage>(reader.GetFieldValue<byte[]>(
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

        return result;
    }

    public async Task<List<PostgresMessage<DictionaryMessage>>> PeekDeadLettersAsync(PostgresQueueName queueName, int count, CancellationToken ct)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
SELECT message_id, enqueued_at, created_at, message, properties
FROM {SchemaName}.{DlQueuePrefix}_{queueName}
ORDER BY message_id ASC
LIMIT ($1);
");

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await ReadDeadLetterRows(reader, ct);
    }

    public async Task<List<PostgresMessage<DictionaryMessage>>> ReadDeadLettersAsync(PostgresQueueName queueName, int count, CancellationToken ct)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
WITH deleted_rows AS (
    DELETE FROM {SchemaName}.{DlQueuePrefix}_{queueName}  
    WHERE message_id IN (
        SELECT message_id
        FROM {SchemaName}.{DlQueuePrefix}_{queueName}  
        ORDER BY message_id ASC
        LIMIT ($1)
        FOR UPDATE
    )
    RETURNING *
)
SELECT message_id, enqueued_at, created_at, message, properties
FROM deleted_rows;
");

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await ReadDeadLetterRows(reader, ct);
    }

    public async Task<long> RequeueDeadLettersAsync(PostgresQueueName queueName, int count, CancellationToken ct)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
WITH deleted_rows AS (
    DELETE FROM {SchemaName}.{DlQueuePrefix}_{queueName}
    WHERE message_id IN (
        SELECT message_id
        FROM {SchemaName}.{DlQueuePrefix}_{queueName}  
        ORDER BY message_id ASC
        LIMIT ($1)
        FOR UPDATE
    )
    RETURNING *
), inserted_rows AS (  
    INSERT INTO {SchemaName}.{QueuePrefix}_{queueName} (visibility_timeout, message) 
    SELECT now(), message  
    FROM deleted_rows  
    RETURNING *
)
SELECT COUNT(*) FROM inserted_rows;
");
        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        var result = await command.ExecuteScalarAsync(ct);
        return (long)(result ?? 0);
    }

    public async Task DeleteQueue(PostgresQueueName queueName, CancellationToken ct)
    {
        await using var connection = await _npgsqlDataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        await using var truncateQueue = _npgsqlDataSource.CreateCommand(@$"
DROP TABLE IF EXISTS {SchemaName}.{QueuePrefix}_{queueName};
");
        await using var truncateDlQueue = _npgsqlDataSource.CreateCommand(@$"
DROP TABLE IF EXISTS {SchemaName}.{DlQueuePrefix}_{queueName};
");
        await using var deleteMetadata = _npgsqlDataSource.CreateCommand(@$"
DELETE FROM {SchemaName}.metadata
WHERE queue_name = ($1);
");
        deleteMetadata.Parameters.Add(new NpgsqlParameter<string> { TypedValue = queueName.Value });

        await truncateQueue.ExecuteNonQueryAsync(ct);
        await truncateDlQueue.ExecuteNonQueryAsync(ct);
        await deleteMetadata.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task PurgeDeadLetterQueue(PostgresQueueName queueName)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
DELETE FROM {SchemaName}.{QueuePrefix}_{queueName};
");
        await command.ExecuteNonQueryAsync();
    }

    public async Task PurgeQueue(PostgresQueueName queueName)
    {
        await using var command = _npgsqlDataSource.CreateCommand(@$"
DELETE FROM {SchemaName}.{QueuePrefix}_{queueName};
");
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<PostgresMessage<DictionaryMessage>>> ReadDeadLetterRows(NpgsqlDataReader reader, CancellationToken ct)
    {
        var result = new List<PostgresMessage<DictionaryMessage>>();
        while (await reader.ReadAsync(ct))
        {
            var propertiesOrdinal = reader.GetOrdinal("properties");
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<DictionaryMessage>
            {
                Id = reader.GetInt64(reader.GetOrdinal("message_id")),
                Message = _serializer
                    .Deserialize<DictionaryMessage>(reader.GetFieldValue<byte[]>(
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

        return result;
    }
}
