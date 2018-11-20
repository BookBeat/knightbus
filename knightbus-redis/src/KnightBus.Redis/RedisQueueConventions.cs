namespace KnightBus.Redis
{
    public static class RedisQueueConventions
    {
        public static string GetHashKey(string id, string queueName) => $"{queueName}:{id}";
        public static string GetDeadLetterQueueName(string queueName) => $"{queueName}:dl";
    }
}