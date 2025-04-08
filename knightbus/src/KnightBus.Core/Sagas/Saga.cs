using System;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Sagas;

public interface ISaga
{
    /// <summary>
    /// Partition key for the Saga. Must be the same for all sagas of a specific type. Used to partition storage.
    /// </summary>
    string PartitionKey { get; }

    /// <summary>
    /// Unique id for the instance of the Saga. Corresponds to the <see cref="SagaMessageMapper"/>
    /// </summary>
    string Id { get; set; }
    ISagaMessageMapper MessageMapper { get; }
}

public interface ISaga<T> : ISaga
{
    /// <summary>
    /// Stateful data associated with the Saga.
    /// </summary>
    ///
    SagaData<T> SagaData { set; }

    ISagaStore SagaStore { set; }

    TimeSpan TimeToLive { get; }

    /// <summary>
    /// Mark the Saga as completed.
    /// </summary>
    Task CompleteAsync(CancellationToken ct);

    /// <summary>
    /// Update the Saga Data.
    /// </summary>
    Task UpdateAsync(CancellationToken ct);
}

public class SagaData<T>
{
    public T Data { get; set; }
    public string ConcurrencyStamp { get; set; }
}

public abstract class Saga<T> : ISaga<T>
{
    public abstract string PartitionKey { get; }
    public string Id { get; set; }
    public SagaData<T> SagaData { private get; set; }
    public T Data
    {
        get { return SagaData.Data; }
        set { SagaData.Data = value; }
    }
    public ISagaMessageMapper MessageMapper { get; } = new SagaMessageMapper();
    public ISagaStore SagaStore { get; set; }

    public abstract TimeSpan TimeToLive { get; }

    public virtual Task CompleteAsync(CancellationToken ct)
    {
        return SagaStore.Complete(PartitionKey, Id, SagaData, ct);
    }

    public virtual Task UpdateAsync(CancellationToken ct)
    {
        return SagaStore.Update(PartitionKey, Id, SagaData, ct);
    }
}
