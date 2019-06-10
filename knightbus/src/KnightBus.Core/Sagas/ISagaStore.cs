using System.Threading.Tasks;

namespace KnightBus.Core.Sagas
{
    public interface ISagaStore 
    {
        Task<T> GetSaga<T>(string name, string id);
        Task<T> Create<T>(string id, T sagaData);
        Task Update<T>(string id, T sagaData);
        Task Complete(string name, string id);
        Task Fail(string name, string id);
    }
}