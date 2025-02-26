using KnightBus.Messages;

namespace KnightBus.Cosmos;

public class CosmosMessage<T> where T : class, IMessage
{
    public long Id { get; init; }
    public required T Message { get; init; }
    public int ReadCount { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}
