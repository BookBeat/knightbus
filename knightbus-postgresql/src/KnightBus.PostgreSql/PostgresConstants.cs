namespace KnightBus.PostgreSql;

public static class PostgresConstants
{
    public const string QueuePrefix = "q";
    public const string TopicPrefix = "t";
    public const string SubscriptionPrefix = "s";
    public const string DlQueuePrefix = "dlq";
    public const string SchemaName = "knightbus";
    
    internal const string NpgsqlDataSourceContainerKey = "knightbus-postgres";
}
