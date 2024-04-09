using KnightBus.PostgreSql.Messages;

namespace KnightBus.PostgreSql;

public class PostgresMessage<T> where T : class, IPostgresCommand
{
    public long Id { get; init; }
    public required T Message { get; init; }
    public int ReadCount { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}
