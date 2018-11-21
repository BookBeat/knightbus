namespace KnightBus.Redis
{
    public static class RedisQueueConventions
    {
        public static string GetHashKey(string queueName, string id) => $"{queueName}:msg:{id}";
        public static string GetDeadLetterQueueName(string queueName) => $"{queueName}:deadletter";
        public static string GetSubscriptionKey(string queueName) => $"{queueName}:subs";
        public static string GetSubscriptionQueueName(string queueName, string subscription) => $"{queueName}:subs:{subscription}";
        public static string GetAttachmentMetadataKey(string queueName, string id) => $"{RedisQueueConventions.GetHashKey(queueName, id)}:attachment:metadata";
        public static string GetAttachmentBinaryKey(string queueName, string id) => $"{RedisQueueConventions.GetHashKey(queueName, id)}:attachment:binary";
    }
}