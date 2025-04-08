using KnightBus.Messages;

namespace KnightBus.Azure.Storage.Messages;

/// <summary>
/// Uses the Azure Storage Queues for transport. Suitable for long running low throughput commands
/// </summary>
public interface IStorageQueueCommand : ICommand { }
