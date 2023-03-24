namespace KnightBus.Messages
{
    /// <summary>
    /// Implement this for <see cref="IMessageMapping"/> to be use a specific serializer
    /// </summary>
    public interface ICustomMessageSerializer
    {
        IMessageSerializer MessageSerializer { get; }
    }
}
