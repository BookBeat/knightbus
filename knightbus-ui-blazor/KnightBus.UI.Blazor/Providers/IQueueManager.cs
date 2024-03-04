namespace KnightBus.UI.Blazor.Providers;

public interface IQueueManager
{
    Task<IEnumerable<QueueProperties>> List(CancellationToken ct);
    Task<QueueProperties> Get(string path, CancellationToken ct);

    /// <summary>
    /// Delete queue or subscription
    /// </summary>
    /// <param name="path"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task Delete(string path, CancellationToken ct);
    Task<IReadOnlyList<QueueMessage>> Peek(string name, int count, CancellationToken ct);
    Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string name, int count, CancellationToken ct);
    Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(string name, int count, CancellationToken ct);
    Task<int> MoveDeadLetters(string name, int count, CancellationToken ct);
    string DisplayName { get; }
    QueueType QueueType { get; }
}

public record QueueMessage(
    string Body,
    object Error,
    DateTimeOffset? Time,
    int DeliveryCount,
    string MessageId)
{
    public bool ShowDetails { get; set; }
};
