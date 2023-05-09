namespace KnightBus.Messages
{
    /// <summary>
    /// Base interface for mapping messages to queues.
    /// </summary>
    public interface IMessageMapping
    {
        string QueueName { get; }
    }

    /// <summary>
    /// Maps a specific <see cref="IMessage"/> to a queue.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMessageMapping<T> : IMessageMapping where T : IMessage
    {
    }
}
