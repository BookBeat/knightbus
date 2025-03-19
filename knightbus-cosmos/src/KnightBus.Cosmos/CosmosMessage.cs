using KnightBus.Messages;

namespace KnightBus.Cosmos;
//TODO : Currently not used, InternalCosmosMessage prob should be moved here
public class CosmosMessage<T> where T : class, IMessage
{
    public long Id { get; init; }
    public required T Message { get; init; }
    public int ReadCount { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}
