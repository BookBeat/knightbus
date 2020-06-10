using System;
using System.Threading.Tasks;

namespace KnightBus.Core.Sagas
{
    public interface ISagaStore
    {
        Task<T> GetSaga<T>(string partitionKey, string id);
        Task<T> Create<T>(string partitionKey, string id, T sagaData, TimeSpan ttl);
        Task Update<T>(string partitionKey, string id, T sagaData);
        Task Complete(string partitionKey, string id);
    }
}