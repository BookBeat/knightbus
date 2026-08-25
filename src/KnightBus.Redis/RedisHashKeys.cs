namespace KnightBus.Redis;

public static class RedisHashKeys
{
    public const string LastProcessed = "lpt";
    public const string DeliveryCount = "dcount";
    public const string Errors = "errors";
}
