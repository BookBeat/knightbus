using Npgsql;

namespace KnightBus.PostgreSql;

internal static class NpgsqlBatchCommandCollectionExtensions
{
    public static void AddRange(
        this NpgsqlBatchCommandCollection batch,
        params IEnumerable<NpgsqlBatchCommand> command
    )
    {
        foreach (var batchCommand in command)
        {
            batch.Add(batchCommand);
        }
    }
}
