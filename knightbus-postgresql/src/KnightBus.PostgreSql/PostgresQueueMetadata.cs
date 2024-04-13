namespace KnightBus.PostgreSql;

public record PostgresQueueMetadata
{
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int ActiveMessagesCount { get; set; }
    public int DeadLetterMessagesCount { get; set; }
}
