using Azure.Messaging.ServiceBus;

namespace KnightBus.UI.Console.Providers;

public interface IQueueManager
{
    IEnumerable<QueueProperties> List(CancellationToken ct);
    Task<QueueProperties> Get(string path, CancellationToken ct);
    Task Delete(string path, CancellationToken ct);
    Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string name, int count, CancellationToken ct);
    Task<int> MoveDeadLetters(string name, int count, CancellationToken ct);
}

public record QueueMessage(string Body, object Error, DateTimeOffset Time);
