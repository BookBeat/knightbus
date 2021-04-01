namespace KnightBus.Messages
{
    /// <summary>
    /// Explicit serialization setting for the <see cref="IMessageMapping"/>
    /// </summary>
    public interface IMessageSerialization
    {
        string ContentType { get; }
    }
}