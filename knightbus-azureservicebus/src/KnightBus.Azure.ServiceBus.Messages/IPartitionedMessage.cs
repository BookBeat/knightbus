namespace KnightBus.Azure.ServiceBus.Messages
{
    /// <summary>
    /// Indicates the queue/topic should be partitioned across multiple message brokers.
    /// </summary>
    public interface IPartitionedMessage { }
}