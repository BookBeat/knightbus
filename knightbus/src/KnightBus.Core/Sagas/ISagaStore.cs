using System.Threading.Tasks;

namespace KnightBus.Core.Sagas
{
    public interface ISagaStore 
    {
        Task<T> GetSaga<T>(string sagaId, string instanceId);
        Task<T> Create<T>(string sagaId, string instanceId, T sagaData);
        Task Update<T>(string sagaId, string instanceId, T sagaData);
        Task Complete(string sagaId, string instanceId);
        Task Fail(string sagaId, string instanceId);
    }
}