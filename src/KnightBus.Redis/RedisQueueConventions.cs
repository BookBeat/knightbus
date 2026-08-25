namespace KnightBus.Redis;

public static class RedisQueueConventions
{
    public static string QueueListKey => "queues";
    public static string TopicListKey => "topics";

    public static string GetMessageHashKey(string queueName, string id) => $"{queueName}:msg:{id}";

    public static string GetProcessingQueueName(string queueName) => $"{queueName}:processing";

    public static string GetDeadLetterQueueName(string queueName) => $"{queueName}:deadletter";

    public static string GetSubscriptionKey(string queueName) => $"{queueName}:subs";

    public static string GetSubscriptionQueueName(string queueName, string subscription) =>
        $"{queueName}:subs:{subscription}";

    public static string GetSagaKey(string partitionKey, string id) => $"sagas:{partitionKey}:{id}";
}
