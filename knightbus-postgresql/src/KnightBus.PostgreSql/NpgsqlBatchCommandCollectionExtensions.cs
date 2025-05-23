using Npgsql;

namespace KnightBus.PostgreSql;

internal static class NpgsqlBatchCommandCollectionExtensions
{
    public static void AddRange(
        this NpgsqlBatchCommandCollection collection,
        params IEnumerable<NpgsqlBatchCommand> commands
    )
    {
        foreach (var command in commands)
        {
            collection.Add(command);
        }
    }
}
