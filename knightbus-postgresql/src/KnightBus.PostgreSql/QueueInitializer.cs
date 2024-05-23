using Npgsql;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public static class QueueInitializer
{

    public static async Task InitSubscription(PostgresQueueName topic, PostgresQueueName subscription, NpgsqlDataSource npgsqlDataSource)
    {
        await using var connection = await npgsqlDataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var createSchema = new NpgsqlCommand(@$"
 CREATE SCHEMA IF NOT EXISTS {SchemaName};
", connection);

        var topicSubscriptionQueueName = PostgresQueueName.Create($"{topic}_{subscription}");

        await using var createPublishFunctionCmd = CreatePublishFunction(connection);
        await using var createTopicCmd = CreateTopicTableCmd(topic, connection);
        await using var insertTopicCmd = InsertTopicSubscriptionCmd(topic, topicSubscriptionQueueName, connection);

        await using var createQueueCmd = CreateQueueCmd(SubscriptionPrefix, topicSubscriptionQueueName, connection);
        await using var createIndexCmd = CreateQueueIndexCmd(SubscriptionPrefix, topicSubscriptionQueueName, connection);
        await using var createDlQueueCmd = CreateDlQueueCmd(DlQueuePrefix, topicSubscriptionQueueName, connection);

        await createSchema.ExecuteNonQueryAsync();
        await createTopicCmd.ExecuteNonQueryAsync();
        await insertTopicCmd.ExecuteNonQueryAsync();
        await createQueueCmd.ExecuteNonQueryAsync();
        await createIndexCmd.ExecuteNonQueryAsync();
        await createDlQueueCmd.ExecuteNonQueryAsync();
        await createPublishFunctionCmd.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
    }

    public static async Task InitQueue(PostgresQueueName queueName, NpgsqlDataSource npgsqlDataSource)
    {
        await using var connection = await npgsqlDataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var createSchema = new NpgsqlCommand(@$"
 CREATE SCHEMA IF NOT EXISTS {SchemaName};
", connection);

        await using var createQueueCmd = CreateQueueCmd(QueuePrefix, queueName, connection);
        await using var createDlQueueCmd = CreateDlQueueCmd(DlQueuePrefix, queueName, connection);
        await using var createIndexCmd = CreateQueueIndexCmd(QueuePrefix, queueName, connection);
        await using var createMetadataTableCmd = CreateMetadataTableCmd(connection);
        await using var insertMetadataCmd = InsertMetadataCmd(queueName, connection);

        await createSchema.ExecuteNonQueryAsync();
        await createQueueCmd.ExecuteNonQueryAsync();
        await createDlQueueCmd.ExecuteNonQueryAsync();
        await createIndexCmd.ExecuteNonQueryAsync();
        await createMetadataTableCmd.ExecuteNonQueryAsync();
        await insertMetadataCmd.ExecuteNonQueryAsync();

        await transaction.CommitAsync();
    }


    private static NpgsqlCommand CreateTopicTableCmd(PostgresQueueName topic, NpgsqlConnection connection)
    {
        var createTopicTableCmd = new NpgsqlCommand($@"
CREATE TABLE IF NOT EXISTS {SchemaName}.{TopicPrefix}_{topic} (
    subscription_name VARCHAR UNIQUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL
);", connection);
        return createTopicTableCmd;
    }

    private static NpgsqlCommand InsertTopicSubscriptionCmd(PostgresQueueName topic, PostgresQueueName topicSubscription, NpgsqlConnection connection)
    {
        var insertMetadataCmd = new NpgsqlCommand(@$"
INSERT INTO {SchemaName}.{TopicPrefix}_{topic}(subscription_name)
VALUES ('{topicSubscription}')
ON CONFLICT
DO NOTHING;",
            connection);
        return insertMetadataCmd;
    }

    private static NpgsqlCommand CreatePublishFunction(NpgsqlConnection connection)
    {
        var publishFunction = new NpgsqlCommand(@$"
CREATE OR REPLACE FUNCTION {SchemaName}.publish_events(
    topic TEXT,
    messages JSONB[]
)
RETURNS VOID AS $$
DECLARE
    subscription_name TEXT;
BEGIN
    FOR subscription_name IN
        EXECUTE format('SELECT subscription_name FROM %I.t_%I', '{SchemaName}', topic_table_name)
    LOOP      

        -- Insert all messages into the queue table in a single statement
        EXECUTE format('
            INSERT INTO %I.s_%I_%I (visibility_timeout, message)
            SELECT now(), unnest($1)
        ', '{SchemaName}', topic, subscription_name) USING messages;
    END LOOP;
END;
$$ LANGUAGE plpgsql;",
            connection);
        return publishFunction;
    }

    private static NpgsqlCommand InsertMetadataCmd(PostgresQueueName queueName, NpgsqlConnection connection)
    {
        var insertMetadataCmd = new NpgsqlCommand(@$"
INSERT INTO {SchemaName}.metadata (queue_name)
VALUES ('{queueName}')
ON CONFLICT
DO NOTHING;",
                connection);
        return insertMetadataCmd;
    }

    private static NpgsqlCommand CreateMetadataTableCmd(NpgsqlConnection connection)
    {
        var createMetadataTableCmd = new NpgsqlCommand($@"
CREATE TABLE IF NOT EXISTS {SchemaName}.metadata (
    queue_name VARCHAR UNIQUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL
);", connection);
        return createMetadataTableCmd;
    }

    private static NpgsqlCommand CreateQueueIndexCmd(string prefix, PostgresQueueName queueName, NpgsqlConnection connection)
    {
        var createIndexCmd = new NpgsqlCommand(@$"
CREATE INDEX IF NOT EXISTS {SchemaName}_{prefix}_{queueName}_visibility_timeout_idx
ON {SchemaName}.{prefix}_{queueName} (visibility_timeout ASC);",
                connection);
        return createIndexCmd;
    }

    private static NpgsqlCommand CreateDlQueueCmd(string prefix, PostgresQueueName queueName, NpgsqlConnection connection)
    {
        var createDlQueueCmd = new NpgsqlCommand(@$"
CREATE TABLE IF NOT EXISTS {SchemaName}.{prefix}_{queueName} (
    message_id BIGINT PRIMARY KEY,
    enqueued_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    message JSONB,
    properties JSONB
);", connection);
        return createDlQueueCmd;
    }

    private static NpgsqlCommand CreateQueueCmd(string prefix, PostgresQueueName queueName, NpgsqlConnection connection)
    {
        var createQueueCmd = new NpgsqlCommand(@$"
CREATE TABLE IF NOT EXISTS {SchemaName}.{prefix}_{queueName} (
    message_id BIGINT PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    read_count SMALLINT DEFAULT 0 NOT NULL,
    enqueued_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    visibility_timeout TIMESTAMP WITH TIME ZONE NOT NULL,
    message JSONB,
    properties JSONB
);", connection);
        return createQueueCmd;
    }
}
