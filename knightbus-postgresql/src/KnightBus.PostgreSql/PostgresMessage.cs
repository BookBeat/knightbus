using KnightBus.PostgreSql.Messages;

namespace KnightBus.PostgreSql;

public class PostgresMessage<T> where T : class, IPostgresCommand
{
    public long Id { get; set; }
    public T Message { get; set; }
    public int ReadCount { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}
