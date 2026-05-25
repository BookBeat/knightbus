namespace KnightBus.Deduplication.Redis;

public class RedisDeduplicationOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "knightbus:dedup:";
}
