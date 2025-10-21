using System.Runtime.CompilerServices;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql.Management;

public class PostgresManagementClient
{
    private readonly NpgsqlDataSource _npgsqlDataSource;
    private readonly IMessageSerializer _serializer;

    public PostgresManagementClient(
        [FromKeyedServices(NpgsqlDataSourceContainerKey)] NpgsqlDataSource npgsqlDataSource,
        IPostgresConfiguration configuration
    )
    {
        _npgsqlDataSource = npgsqlDataSource;
        _serializer = configuration.MessageSerializer;
    }

    public async Task<List<PostgresQueueMetadata>> ListQueues(CancellationToken ct)
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
SELECT queue_name, created_at
FROM {SchemaName}.metadata;
"
        );

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var queueMetas = new List<PostgresQueueMetadata>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            queueMetas.Add(
                new PostgresQueueMetadata
                {
                    Name = reader.GetString(0),
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(1),
                }
            );
        }

        await using var conn = await _npgsqlDataSource
            .OpenConnectionAsync(ct)
            .ConfigureAwait(false);
        foreach (var queueMetadata in queueMetas)
        {
            await using var batch = new NpgsqlBatch(conn)
            {
                BatchCommands =
                {
                    new NpgsqlBatchCommand(
                        $@"
SELECT COUNT(*) FROM {SchemaName}.{QueuePrefix}_{queueMetadata.Name};"
                    ),
                    new NpgsqlBatchCommand(
                        $@"
SELECT COUNT(*) FROM {SchemaName}.{DlQueuePrefix}_{queueMetadata.Name};"
                    ),
                },
            };

            await batch.PrepareAsync(ct);

            await using var batchReader = await batch.ExecuteReaderAsync(ct).ConfigureAwait(false);
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

    public async Task<List<PostgresQueueMetadata>> ListSubscriptions(
        string topic,
        CancellationToken ct
    )
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
SELECT subscription_name, created_at
FROM {SchemaName}.{TopicPrefix}_{topic};
"
        );

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var queueMetas = new List<PostgresQueueMetadata>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            queueMetas.Add(
                new PostgresQueueMetadata
                {
                    Name = reader.GetString(0),
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(1),
                }
            );
        }

        await using var conn = await _npgsqlDataSource
            .OpenConnectionAsync(ct)
            .ConfigureAwait(false);
        foreach (var queueMetadata in queueMetas)
        {
            await using var batch = new NpgsqlBatch(conn)
            {
                BatchCommands =
                {
                    new NpgsqlBatchCommand(
                        $@"
SELECT COUNT(*) FROM {SchemaName}.{SubscriptionPrefix}_{topic}_{queueMetadata.Name};"
                    ),
                    new NpgsqlBatchCommand(
                        $@"
SELECT COUNT(*) FROM {SchemaName}.{DlQueuePrefix}_{topic}_{queueMetadata.Name};"
                    ),
                },
            };

            await batch.PrepareAsync(ct);

            await using var batchReader = await batch.ExecuteReaderAsync(ct).ConfigureAwait(false);
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

    public async Task<List<PostgresQueueMetadata>> ListTopics(CancellationToken ct)
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
SELECT tablename
FROM pg_catalog.pg_tables
WHERE schemaname = '{SchemaName}' AND tablename LIKE '{TopicPrefix}_%';

"
        );

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var queueMetas = new List<PostgresQueueMetadata>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            queueMetas.Add(
                new PostgresQueueMetadata { Name = reader.GetString(0)[(TopicPrefix.Length + 1)..] }
            );
        }
        return queueMetas;
    }

    public async Task<PostgresQueueMetadata> GetQueue(
        PostgresQueueName queueName,
        CancellationToken ct
    )
    {
        await using var conn = await _npgsqlDataSource
            .OpenConnectionAsync(ct)
            .ConfigureAwait(false);
        await using var batch = new NpgsqlBatch(conn)
        {
            BatchCommands =
            {
                new NpgsqlBatchCommand(
                    $@"
SELECT COUNT(*) FROM {SchemaName}.{QueuePrefix}_{queueName};"
                ),
                new NpgsqlBatchCommand(
                    $@"
SELECT COUNT(*) FROM {SchemaName}.{DlQueuePrefix}_{queueName};"
                ),
            },
        };

        var metadataCommand = new NpgsqlBatchCommand(
            $@"
SELECT created_at FROM {SchemaName}.metadata
WHERE queue_name = ($1);"
        );

        metadataCommand.Parameters.Add(
            new NpgsqlParameter<string> { TypedValue = queueName.Value }
        );
        batch.BatchCommands.Add(metadataCommand);

        await batch.PrepareAsync(ct);

        await using var reader = await batch.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await GetQueueMetadata(reader, queueName, ct);
    }

    private static async Task<PostgresQueueMetadata> GetQueueMetadata(
        NpgsqlDataReader reader,
        PostgresQueueName queueName,
        CancellationToken ct
    )
    {
        var queueMeta = new PostgresQueueMetadata { Name = queueName.Value };
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

    public async Task<PostgresQueueMetadata> GetSubscription(
        string topic,
        PostgresQueueName subscription,
        CancellationToken ct
    )
    {
        await using var conn = await _npgsqlDataSource
            .OpenConnectionAsync(ct)
            .ConfigureAwait(false);
        await using var batch = new NpgsqlBatch(conn)
        {
            BatchCommands =
            {
                new NpgsqlBatchCommand(
                    $@"
SELECT COUNT(*) FROM {SchemaName}.{SubscriptionPrefix}_{topic}_{subscription};"
                ),
                new NpgsqlBatchCommand(
                    $@"
SELECT COUNT(*) FROM {SchemaName}.{DlQueuePrefix}_{topic}_{subscription};"
                ),
            },
        };

        var metadataCommand = new NpgsqlBatchCommand(
            $@"
SELECT created_at FROM {SchemaName}.{TopicPrefix}_{topic}
WHERE subscription_name = ($1);"
        );

        metadataCommand.Parameters.Add(
            new NpgsqlParameter<string> { TypedValue = subscription.Value }
        );
        batch.BatchCommands.Add(metadataCommand);

        await batch.PrepareAsync(ct);

        await using var reader = await batch.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await GetQueueMetadata(reader, subscription, ct);
    }

    public async IAsyncEnumerable<PostgresMessage<DictionaryMessage>> PeekMessagesAsync(
        PostgresQueueName queueName,
        int count,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
SELECT message_id, enqueued_at, read_count, message, properties
FROM {SchemaName}.{QueuePrefix}_{queueName}
ORDER BY message_id ASC
LIMIT ($1);
"
        );

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await foreach (var p in ReadMessages(reader, false, ct))
        {
            yield return p;
        }
    }

    private async IAsyncEnumerable<PostgresMessage<DictionaryMessage>> ReadMessages(
        NpgsqlDataReader reader,
        bool isDeadLetter,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        var propertiesOrdinal = reader.GetOrdinal("properties");
        var messageIdOrdinal = reader.GetOrdinal("message_id");
        var readCountOrdinal = !isDeadLetter ? reader.GetOrdinal("read_count") : 0;
        var messageOrdinal = reader.GetOrdinal("message");
        var enqueuedAtOrdinal = reader.GetOrdinal("enqueued_at");

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var isPropertiesNull = reader.IsDBNull(propertiesOrdinal);

            var postgresMessage = new PostgresMessage<DictionaryMessage>
            {
                Id = reader.GetInt64(messageIdOrdinal),
                ReadCount = reader.GetInt32(readCountOrdinal),
                Message = _serializer.Deserialize<DictionaryMessage>(
                    reader.GetFieldValue<byte[]>(messageOrdinal).AsMemory()
                ),
                Properties = isPropertiesNull
                    ? new Dictionary<string, string>()
                    : _serializer.Deserialize<Dictionary<string, string>>(
                        reader.GetFieldValue<byte[]>(propertiesOrdinal).AsMemory()
                    ),
                Time = reader.GetDateTime(enqueuedAtOrdinal),
            };
            yield return postgresMessage;
        }
    }

    public async IAsyncEnumerable<PostgresMessage<DictionaryMessage>> PeekMessagesAsync(
        string topic,
        PostgresQueueName subscription,
        int count,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
SELECT message_id, enqueued_at, read_count, message, properties
FROM {SchemaName}.{SubscriptionPrefix}_{topic}_{subscription}
ORDER BY message_id ASC
LIMIT ($1);
"
        );

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await foreach (var p in ReadMessages(reader, false, ct))
        {
            yield return p;
        }
    }

    public async IAsyncEnumerable<PostgresMessage<DictionaryMessage>> PeekDeadLettersAsync(
        PostgresQueueName queueName,
        int count,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
SELECT message_id, enqueued_at, created_at, message, properties
FROM {SchemaName}.{DlQueuePrefix}_{queueName}
ORDER BY message_id ASC
LIMIT ($1);
"
        );

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        await foreach (var p in ReadMessages(reader, true, ct))
        {
            yield return p;
        }
    }

    public async IAsyncEnumerable<PostgresMessage<DictionaryMessage>> PeekDeadLettersAsync(
        string topic,
        PostgresQueueName subscription,
        int count,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
SELECT message_id, enqueued_at, created_at, message, properties
FROM {SchemaName}.{DlQueuePrefix}_{topic}_{subscription}
ORDER BY message_id ASC
LIMIT ($1);
"
        );

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        await foreach (var p in ReadMessages(reader, true, ct))
        {
            yield return p;
        }
    }

    public async IAsyncEnumerable<PostgresMessage<DictionaryMessage>> ReadDeadLettersAsync(
        PostgresQueueName queueName,
        int count,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
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
"
        );

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        await foreach (var p in ReadMessages(reader, true, ct))
        {
            yield return p;
        }
    }

    public async IAsyncEnumerable<PostgresMessage<DictionaryMessage>> ReadDeadLettersAsync(
        string topic,
        PostgresQueueName subscription,
        int count,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
WITH deleted_rows AS (
    DELETE FROM {SchemaName}.{DlQueuePrefix}_{topic}_{subscription}  
    WHERE message_id IN (
        SELECT message_id
        FROM {SchemaName}.{DlQueuePrefix}_{topic}_{subscription}  
        ORDER BY message_id ASC
        LIMIT ($1)
        FOR UPDATE
    )
    RETURNING *
)
SELECT message_id, enqueued_at, created_at, message, properties
FROM deleted_rows;
"
        );

        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        await foreach (var p in ReadMessages(reader, true, ct))
        {
            yield return p;
        }
    }

    public async Task<long> RequeueDeadLettersAsync(
        PostgresQueueName queueName,
        int count,
        CancellationToken ct
    )
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
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
"
        );
        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        var result = await command.ExecuteScalarAsync(ct);
        return (long)(result ?? 0);
    }

    public async Task<long> RequeueDeadLettersAsync(
        string topic,
        PostgresQueueName subscription,
        int count,
        CancellationToken ct
    )
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
WITH deleted_rows AS (
    DELETE FROM {SchemaName}.{DlQueuePrefix}_{topic}_{subscription}
    WHERE message_id IN (
        SELECT message_id
        FROM {SchemaName}.{DlQueuePrefix}_{topic}_{subscription}  
        ORDER BY message_id ASC
        LIMIT ($1)
        FOR UPDATE
    )
    RETURNING *
), inserted_rows AS (  
    INSERT INTO {SchemaName}.{SubscriptionPrefix}_{topic}_{subscription} (visibility_timeout, message) 
    SELECT now(), message  
    FROM deleted_rows  
    RETURNING *
)
SELECT COUNT(*) FROM inserted_rows;
"
        );
        command.Parameters.Add(new NpgsqlParameter<int> { TypedValue = count });
        var result = await command.ExecuteScalarAsync(ct);
        return (long)(result ?? 0);
    }

    public async Task DeleteQueue(PostgresQueueName queueName, CancellationToken ct)
    {
        await using var connection = await _npgsqlDataSource
            .OpenConnectionAsync(ct)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        await using var truncateQueue = _npgsqlDataSource.CreateCommand(
            @$"
DROP TABLE IF EXISTS {SchemaName}.{QueuePrefix}_{queueName};
"
        );
        await using var truncateDlQueue = _npgsqlDataSource.CreateCommand(
            @$"
DROP TABLE IF EXISTS {SchemaName}.{DlQueuePrefix}_{queueName};
"
        );
        await using var deleteMetadata = _npgsqlDataSource.CreateCommand(
            @$"
DELETE FROM {SchemaName}.metadata
WHERE queue_name = ($1);
"
        );
        deleteMetadata.Parameters.Add(new NpgsqlParameter<string> { TypedValue = queueName.Value });

        await truncateQueue.ExecuteNonQueryAsync(ct);
        await truncateDlQueue.ExecuteNonQueryAsync(ct);
        await deleteMetadata.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task DeleteSubscription(
        string topic,
        PostgresQueueName subscription,
        CancellationToken ct
    )
    {
        await using var connection = await _npgsqlDataSource
            .OpenConnectionAsync(ct)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        await using var truncateQueue = _npgsqlDataSource.CreateCommand(
            @$"
DROP TABLE IF EXISTS {SchemaName}.{SubscriptionPrefix}_{topic}_{subscription};
"
        );
        await using var truncateDlQueue = _npgsqlDataSource.CreateCommand(
            @$"
DROP TABLE IF EXISTS {SchemaName}.{DlQueuePrefix}_{topic}_{subscription};
"
        );
        await using var deleteMetadata = _npgsqlDataSource.CreateCommand(
            @$"
DELETE FROM {SchemaName}.{TopicPrefix}_{topic}
WHERE subscription_name = ($1);
"
        );
        deleteMetadata.Parameters.Add(
            new NpgsqlParameter<string> { TypedValue = subscription.Value }
        );

        await truncateQueue.ExecuteNonQueryAsync(ct);
        await truncateDlQueue.ExecuteNonQueryAsync(ct);
        await deleteMetadata.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task PurgeDeadLetterQueue(PostgresQueueName queueName)
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
DELETE FROM {SchemaName}.{DlQueuePrefix}_{queueName};
"
        );
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task PurgeQueue(PostgresQueueName queueName)
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
DELETE FROM {SchemaName}.{QueuePrefix}_{queueName};
"
        );
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task SendMessage(
        PostgresQueueName queueName,
        string jsonBody,
        CancellationToken cancellationToken
    )
    {
        await using var command = _npgsqlDataSource.CreateCommand(
            @$"
INSERT INTO {SchemaName}.{QueuePrefix}_{queueName} (visibility_timeout, message)
VALUES (now(), $1);
"
        );
        command.Parameters.Add(
            new NpgsqlParameter { Value = jsonBody, NpgsqlDbType = NpgsqlDbType.Jsonb }
        );
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SendMessages(
        PostgresQueueName queueName,
        IEnumerable<string> jsonBodies,
        CancellationToken cancellationToken
    )
    {
        string sql =
            //lang=postgresql
            $"COPY {SchemaName}.{QueuePrefix}_{queueName} (visibility_timeout, message) FROM STDIN (FORMAT binary)";

        await using var connection = await _npgsqlDataSource
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var importer = await connection
            .BeginBinaryImportAsync(sql, cancellationToken)
            .ConfigureAwait(false);

        var visibilityTimeout = DateTimeOffset.UtcNow;
        foreach (var jsonBody in jsonBodies)
        {
            await importer.StartRowAsync(cancellationToken).ConfigureAwait(false);
            await importer
                .WriteAsync(visibilityTimeout, NpgsqlDbType.TimestampTz, cancellationToken)
                .ConfigureAwait(false);
            await importer
                .WriteAsync(jsonBody, NpgsqlDbType.Jsonb, cancellationToken)
                .ConfigureAwait(false);
        }

        await importer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
