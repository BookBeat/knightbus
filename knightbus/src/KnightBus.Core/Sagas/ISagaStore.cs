using System;
using System.Threading.Tasks;

namespace KnightBus.Core.Sagas
{
    public interface ISagaStore
    {
        Task<SagaData<T>> GetSaga<T>(string partitionKey, string id);
        Task<SagaData<T>> Create<T>(string partitionKey, string id, T data, TimeSpan ttl);
        Task Update<T>(string partitionKey, string id, SagaData<T> sagaData);
        Task Complete(string partitionKey, string id);
    }
}
