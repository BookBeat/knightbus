using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Management;

public interface IQueueManager
{
    /// <summary>
    /// Lists the queues or subscriptions
    /// </summary>
    Task<IEnumerable<QueueProperties>> List(CancellationToken ct);

    /// <summary>
    /// Gets the queue or subscription properties
    /// </summary>
    Task<QueueProperties> Get(string path, CancellationToken ct);

    /// <summary>
    /// Delete queue or subscription
    /// </summary>
    Task Delete(string path, CancellationToken ct);

    /// <summary>
    /// Peeks messages without removing them from the queue
    /// </summary>
    Task<IReadOnlyList<QueueMessage>> Peek(string name, int count, CancellationToken ct);

    /// <summary>
    /// Peeks dead letter messages without removing them from the queue
    /// </summary>
    Task<IReadOnlyList<QueueMessage>> PeekDeadLetter(string path, int count, CancellationToken ct);

    /// <summary>
    /// Reads dead letter messages and removes them from the queue
    /// </summary>
    Task<IReadOnlyList<QueueMessage>> ReadDeadLetter(string path, int count, CancellationToken ct);

    /// <summary>
    /// Moves dead letter messages back to the original queue
    /// </summary>
    Task<int> MoveDeadLetters(string path, int count, CancellationToken ct);

    /// <summary>
    /// Type of queue <see cref="QueueType"/>
    /// </summary>
    QueueType QueueType { get; }
}
