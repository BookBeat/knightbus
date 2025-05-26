using Npgsql;
using static KnightBus.PostgreSql.PostgresConstants;

namespace KnightBus.PostgreSql;

public static class QueueInitializer
{
    // CREATE IF NOT EXISTS is not thread-safe
    //Semaphore used to ensure that setup of each subscriber/command sets up sequentially.
    private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
    
    public static async Task InitSubscription(
        PostgresQueueName topic,
        PostgresQueueName subscription,
        NpgsqlDataSource npgsqlDataSource
    )
    {
        await Semaphore.WaitAsync();
        
        await using var connection = await npgsqlDataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var createSchema = new NpgsqlCommand(
            @$"
 CREATE SCHEMA IF NOT EXISTS {SchemaName};
",
            connection
        );

        var topicSubscriptionQueueName = PostgresQueueName.Create($"{topic}_{subscription}");

        await using var createPublishFunctionCmd = CreatePublishFunction(connection, transaction);
        await using var createTopicCmd = CreateTopicTableCmd(topic, connection, transaction);
        await using var insertTopicCmd = InsertTopicSubscriptionCmd(
            topic,
            subscription,
            connection,
            transaction
        );

        await using var createQueueCmd = CreateQueueCmd(
            SubscriptionPrefix,
            topicSubscriptionQueueName,
            connection,
            transaction
        );
        await using var createIndexCmd = CreateQueueIndexCmd(
            SubscriptionPrefix,
            topicSubscriptionQueueName,
            connection,
            transaction
        );
        await using var createDlQueueCmd = CreateDlQueueCmd(
            DlQueuePrefix,
            topicSubscriptionQueueName,
            connection,
            transaction
        );
        
        try
        {
            await createSchema.ExecuteNonQueryAsync().ConfigureAwait(false);
            await createTopicCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            await insertTopicCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            await createQueueCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            await createIndexCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            await createDlQueueCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            await createPublishFunctionCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine($"SQL setup failed for sub {subscription} - {e.Message}");
            
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                Console.WriteLine($"Rollback failed {rollbackException.Message}");
            }
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
        await Semaphore.WaitAsync();
        await using var connection = await npgsqlDataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var createSchema = new NpgsqlCommand(
            @$"
 CREATE SCHEMA IF NOT EXISTS {SchemaName};
",
            connection, transaction
        );
        
        await using var createQueueCmd = CreateQueueCmd(QueuePrefix, queueName, connection, transaction);
        await using var createDlQueueCmd = CreateDlQueueCmd(DlQueuePrefix, queueName, connection, transaction);
        await using var createIndexCmd = CreateQueueIndexCmd(QueuePrefix, queueName, connection, transaction);
        await using var createMetadataTableCmd = CreateMetadataTableCmd(connection, transaction);
        await using var insertMetadataCmd = InsertMetadataCmd(queueName, connection, transaction);

        try
        {
            await createSchema.ExecuteNonQueryAsync().ConfigureAwait(false);
            await createQueueCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            await createDlQueueCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            await createIndexCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            await createMetadataTableCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            await insertMetadataCmd.ExecuteNonQueryAsync().ConfigureAwait(false);

            await transaction.CommitAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine($"SQL setup failed for queue {queueName} - {e.Message}");

            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                Console.WriteLine($"Rollback failed {rollbackException.Message}");
            }
        }

        Semaphore.Release();
    }

    private static NpgsqlCommand CreateTopicTableCmd(
        PostgresQueueName topic,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction
    )
    {
        var createTopicTableCmd = new NpgsqlCommand(
            $@"
CREATE TABLE IF NOT EXISTS {SchemaName}.{TopicPrefix}_{topic} (
    subscription_name VARCHAR UNIQUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL
);",
            connection, transaction
        );
        return createTopicTableCmd;
    }

    private static NpgsqlCommand InsertTopicSubscriptionCmd(
        PostgresQueueName topic,
        PostgresQueueName topicSubscription,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction
    )
    {
        var insertMetadataCmd = new NpgsqlCommand(
            @$"
INSERT INTO {SchemaName}.{TopicPrefix}_{topic}(subscription_name)
VALUES ('{topicSubscription}')
ON CONFLICT
DO NOTHING;",
            connection, transaction
        );
        return insertMetadataCmd;
    }

    private static NpgsqlCommand CreatePublishFunction(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var publishFunction = new NpgsqlCommand(
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
$$ LANGUAGE plpgsql;",
            connection, transaction
        );
        return publishFunction;
    }

    private static NpgsqlCommand InsertMetadataCmd(
        PostgresQueueName queueName,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction
    )
    {
        var insertMetadataCmd = new NpgsqlCommand(
            @$"
INSERT INTO {SchemaName}.metadata (queue_name)
VALUES ('{queueName}')
ON CONFLICT
DO NOTHING;",
            connection
        );
        return insertMetadataCmd;
    }

    private static NpgsqlCommand CreateMetadataTableCmd(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var createMetadataTableCmd = new NpgsqlCommand(
            $@"
CREATE TABLE IF NOT EXISTS {SchemaName}.metadata (
    queue_name VARCHAR UNIQUE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL
);",
            connection
        );
        return createMetadataTableCmd;
    }

    private static NpgsqlCommand CreateQueueIndexCmd(
        string prefix,
        PostgresQueueName queueName,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction
    )
    {
        var createIndexCmd = new NpgsqlCommand(
            @$"
CREATE INDEX IF NOT EXISTS {SchemaName}_{prefix}_{queueName}_visibility_timeout_idx
ON {SchemaName}.{prefix}_{queueName} (visibility_timeout ASC);",
            connection, transaction
        );
        return createIndexCmd;
    }

    private static NpgsqlCommand CreateDlQueueCmd(
        string prefix,
        PostgresQueueName queueName,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction
    )
    {
        var createDlQueueCmd = new NpgsqlCommand(
            @$"
CREATE TABLE IF NOT EXISTS {SchemaName}.{prefix}_{queueName} (
    message_id BIGINT PRIMARY KEY,
    enqueued_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    message JSONB,
    properties JSONB
);",
            connection, transaction
        );
        return createDlQueueCmd;
    }

    private static NpgsqlCommand CreateQueueCmd(
        string prefix,
        PostgresQueueName queueName,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction
    )
    {
        var createQueueCmd = new NpgsqlCommand(
            @$"
CREATE TABLE IF NOT EXISTS {SchemaName}.{prefix}_{queueName} (
    message_id BIGINT PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    read_count SMALLINT DEFAULT 0 NOT NULL,
    enqueued_at TIMESTAMP WITH TIME ZONE DEFAULT now() NOT NULL,
    visibility_timeout TIMESTAMP WITH TIME ZONE NOT NULL,
    message JSONB,
    properties JSONB
);",
            connection, transaction
        );
        return createQueueCmd;
    }
}
