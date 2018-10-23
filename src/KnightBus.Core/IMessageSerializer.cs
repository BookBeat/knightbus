namespace KnightBus.Core
{
    /// <summary>
    /// Determines how messages are serialized when transported
    /// </summary>
    public interface IMessageSerializer
    {
        string Serialize<T>(T message);
        T Deserialize<T>(string serializedString);
        string ContentType { get; }
    }
}