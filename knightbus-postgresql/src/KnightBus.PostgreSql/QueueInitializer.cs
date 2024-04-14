using Npgsql;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public static class QueueInitializer
{
    public static async Task InitQueue(PostgresQueueName queueName, NpgsqlDataSource npgsqlDataSource)
    {
        await using var connection = await npgsqlDataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var createSchema = new NpgsqlCommand(@$"
 CREATE SCHEMA IF NOT EXISTS {SchemaName};
", connection);

        await using var createQueueCmd = new NpgsqlCommand(@$"
CREATE TABLE IF NOT EXISTS {SchemaName}.{QueuePrefix}_{queueName} (
    message_id BIGINT PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    read_count INT DEFAULT 0 NOT NULL,
    enqueued_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    visibility_timeout TIMESTAMP WITH TIME ZONE NOT NULL,
    message JSONB,
    properties JSONB
);", connection);

        await using var createDlQueueCmd = new NpgsqlCommand(@$"
CREATE TABLE IF NOT EXISTS {SchemaName}.{DlQueuePrefix}_{queueName} (
    message_id BIGINT PRIMARY KEY,
    enqueued_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    message JSONB,
    properties JSONB
);", connection);

        await using var createIndexCmd = new NpgsqlCommand(@$"
CREATE INDEX IF NOT EXISTS {SchemaName}_{QueuePrefix}_{queueName}_visibility_timeout_idx
ON {SchemaName}.{QueuePrefix}_{queueName} (visibility_timeout ASC);",
            connection);

        await using var createMetadataTableCmd = new NpgsqlCommand($@"
CREATE TABLE IF NOT EXISTS {SchemaName}.metadata (
    queue_name VARCHAR UNIQUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL
);", connection);

        await using var insertMetadataCmd = new NpgsqlCommand(@$"
INSERT INTO {SchemaName}.metadata (queue_name)
VALUES ('{queueName}')
ON CONFLICT
DO NOTHING;",
            connection);

        await createSchema.ExecuteNonQueryAsync();
        await createQueueCmd.ExecuteNonQueryAsync();
        await createDlQueueCmd.ExecuteNonQueryAsync();
        await createIndexCmd.ExecuteNonQueryAsync();
        await createMetadataTableCmd.ExecuteNonQueryAsync();
        await insertMetadataCmd.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
    }
}
