using System.Threading.Tasks;

namespace KnightBus.Core.Sagas
{
    public interface ISagaStore 
    {
        Task<T> GetSaga<T>(string partitionKey, string id);
        Task<T> Create<T>(string partitionKey, string id, T sagaData);
        Task Update<T>(string partitionKey, string id, T sagaData);
        Task Complete(string partitionKey, string id);
        Task Fail(string partitionKey, string id);
    }
}