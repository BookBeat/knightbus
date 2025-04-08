using KnightBus.Messages;

namespace KnightBus.PostgreSql;

public class PostgresMessage<T> where T : class, IMessage
{
    public long Id { get; init; }
    public required T Message { get; init; }
    public int ReadCount { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
    public DateTimeOffset Time { get; init; }
}
