using System.Transactions;
using Npgsql;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public static class QueueInitializer
{
    // "CREATE IF NOT EXISTS" is not thread-safe
    //Semaphore used to ensure that setup of each subscriber/command sets up sequentially.
    private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

    public static async Task InitSubscription(
        PostgresQueueName topic,
        PostgresQueueName subscription,
        NpgsqlDataSource npgsqlDataSource
    )
    {
        await using var connection = await npgsqlDataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var topicSubscriptionQueueName = PostgresQueueName.Create($"{topic}_{subscription}");

        await Semaphore.WaitAsync();

        try
        {
            await using var batch = new NpgsqlBatch(connection, transaction);
            batch.BatchCommands.AddRange(
                CreateSchemaCmd(),
                CreatePublishFunction(),
                CreateTopicTableCmd(topic),
                InsertTopicSubscriptionCmd(topic, subscription),
                CreateQueueCmd(SubscriptionPrefix, topicSubscriptionQueueName),
                CreateQueueIndexCmd(SubscriptionPrefix, topicSubscriptionQueueName),
                CreateDlQueueCmd(DlQueuePrefix, topicSubscriptionQueueName)
            );
            await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public static async Task InitQueue(
        PostgresQueueName queueName,
        NpgsqlDataSource npgsqlDataSource
    )
    {
        await using var connection = await npgsqlDataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await Semaphore.WaitAsync();
        try
        {
            await using var batch = new NpgsqlBatch(connection, transaction);
            batch.BatchCommands.AddRange(
                CreateSchemaCmd(),
                CreateQueueCmd(QueuePrefix, queueName),
                CreateDlQueueCmd(DlQueuePrefix, queueName),
                CreateQueueIndexCmd(QueuePrefix, queueName),
                CreateMetadataTableCmd(),
                InsertMetadataCmd(queueName)
            );
            await batch.ExecuteNonQueryAsync().ConfigureAwait(false);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private static NpgsqlBatchCommand CreateSchemaCmd()
    {
        return new NpgsqlBatchCommand(
            @$"
 CREATE SCHEMA IF NOT EXISTS {SchemaName};
"
        );
    }

    private static NpgsqlBatchCommand CreateTopicTableCmd(PostgresQueueName topic)
    {
        var createTopicTableCmd = new NpgsqlBatchCommand(
            $@"
CREATE TABLE IF NOT EXISTS {SchemaName}.{TopicPrefix}_{topic} (
    subscription_name VARCHAR UNIQUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL
);"
        );
        return createTopicTableCmd;
    }

    private static NpgsqlBatchCommand InsertTopicSubscriptionCmd(
        PostgresQueueName topic,
        PostgresQueueName topicSubscription
    )
    {
        var insertMetadataCmd = new NpgsqlBatchCommand(
            @$"
INSERT INTO {SchemaName}.{TopicPrefix}_{topic}(subscription_name)
VALUES ('{topicSubscription}')
ON CONFLICT
DO NOTHING;"
        );
        return insertMetadataCmd;
    }

    private static NpgsqlBatchCommand CreatePublishFunction()
    {
        var publishFunction = new NpgsqlBatchCommand(
            @$"
CREATE OR REPLACE FUNCTION {SchemaName}.publish_events(
    topic TEXT,
    messages JSONB[]
)
RETURNS VOID AS $$
DECLARE
    subscription_name TEXT;
BEGIN
    FOR subscription_name IN
        EXECUTE format('SELECT subscription_name FROM %I.t_%I', '{SchemaName}', topic)
    LOOP      

        -- Insert all messages into the queue table in a single statement
        EXECUTE format('
            INSERT INTO %I.s_%I_%I (visibility_timeout, message)
            SELECT now(), unnest($1)
        ', '{SchemaName}', topic, subscription_name) USING messages;
    END LOOP;
END;
$$ LANGUAGE plpgsql;"
        );
        return publishFunction;
    }

    private static NpgsqlBatchCommand InsertMetadataCmd(PostgresQueueName queueName)
    {
        var insertMetadataCmd = new NpgsqlBatchCommand(
            @$"
INSERT INTO {SchemaName}.metadata (queue_name)
VALUES ('{queueName}')
ON CONFLICT
DO NOTHING;"
        );
        return insertMetadataCmd;
    }

    private static NpgsqlBatchCommand CreateMetadataTableCmd()
    {
        var createMetadataTableCmd = new NpgsqlBatchCommand(
            $@"
CREATE TABLE IF NOT EXISTS {SchemaName}.metadata (
    queue_name VARCHAR UNIQUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL
);"
        );
        return createMetadataTableCmd;
    }

    private static NpgsqlBatchCommand CreateQueueIndexCmd(
        string prefix,
        PostgresQueueName queueName
    )
    {
        var createIndexCmd = new NpgsqlBatchCommand(
            @$"
CREATE INDEX IF NOT EXISTS {SchemaName}_{prefix}_{queueName}_visibility_timeout_idx
ON {SchemaName}.{prefix}_{queueName} (visibility_timeout ASC);"
        );
        return createIndexCmd;
    }

    private static NpgsqlBatchCommand CreateDlQueueCmd(string prefix, PostgresQueueName queueName)
    {
        var createDlQueueCmd = new NpgsqlBatchCommand(
            @$"
CREATE TABLE IF NOT EXISTS {SchemaName}.{prefix}_{queueName} (
    message_id BIGINT PRIMARY KEY,
    enqueued_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    message JSONB,
    properties JSONB
);"
        );
        return createDlQueueCmd;
    }

    private static NpgsqlBatchCommand CreateQueueCmd(string prefix, PostgresQueueName queueName)
    {
        var createQueueCmd = new NpgsqlBatchCommand(
            @$"
CREATE TABLE IF NOT EXISTS {SchemaName}.{prefix}_{queueName} (
    message_id BIGINT PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    read_count SMALLINT DEFAULT 0 NOT NULL,
    enqueued_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    visibility_timeout TIMESTAMP WITH TIME ZONE NOT NULL,
    message JSONB,
    properties JSONB
);"
        );
        return createQueueCmd;
    }
}
