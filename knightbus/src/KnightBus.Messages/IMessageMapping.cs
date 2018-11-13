namespace KnightBus.Messages
{
    public interface IMessageMapping
    {
        string QueueName { get; }
    }
    public interface IMessageMapping<T> : IMessageMapping where T : IMessage
    {
    }
}