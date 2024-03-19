using System;
using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core.Sagas;

public interface ISagaStore
{
    Task<SagaData<T>> GetSaga<T>(string partitionKey, string id, CancellationToken ct);
    Task<SagaData<T>> Create<T>(string partitionKey, string id, T data, TimeSpan ttl, CancellationToken ct);
    Task Update<T>(string partitionKey, string id, SagaData<T> sagaData, CancellationToken ct);
    Task Complete<T>(string partitionKey, string id, SagaData<T> sagaData, CancellationToken ct);
    Task Delete(string partitionKey, string id, CancellationToken ct);
}
