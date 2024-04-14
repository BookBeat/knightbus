namespace KnightBus.PostgreSql;
using static PostgresConstants;

public static class Query
{
    public static string GetMessages(PostgresQueueName queueName) => @$"
WITH cte AS
    (
        SELECT message_id
        FROM {SchemaName}.{QueuePrefix}_{queueName}
        WHERE visibility_timeout <= clock_timestamp()
        ORDER BY message_id ASC
        LIMIT ($1)
        FOR UPDATE SKIP LOCKED
    )
UPDATE {SchemaName}.{QueuePrefix}_{queueName} t
    SET
        visibility_timeout = clock_timestamp() + ($2),
        read_count = read_count + 1
        FROM cte
        WHERE t.message_id = cte.message_id
        RETURNING *;
";

    public static string CompleteMessage(PostgresQueueName queueName) => @$"
DELETE FROM {SchemaName}.{QueuePrefix}_{queueName}
WHERE message_id = ($1);
";

    public static string AbandonByError(PostgresQueueName queueName) => @$"
UPDATE {SchemaName}.{QueuePrefix}_{queueName}
SET properties = ($1), visibility_timeout = now()
WHERE message_id = ($2);
";

    public static string DeadLetterMessage(PostgresQueueName queueName) => @$"
WITH DeadLetter AS (
    DELETE FROM {SchemaName}.{QueuePrefix}_{queueName}
    WHERE message_id = ($1)
    RETURNING message_id, enqueued_at, message, properties
)
INSERT INTO {SchemaName}.{DlQueuePrefix}_{queueName} (message_id, enqueued_at, created_at, message, properties)
SELECT message_id, enqueued_at, now(), message, properties
FROM DeadLetter;
";

    public static string PeekDeadLetterMessage(PostgresQueueName queueName) => @$"
SELECT message_id, enqueued_at, created_at, message, properties
FROM {SchemaName}.{DlQueuePrefix}_{queueName}
ORDER BY message_id ASC
LIMIT ($1);
";
}
