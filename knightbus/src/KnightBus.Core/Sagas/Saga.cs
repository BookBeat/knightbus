using System;
using System.Threading.Tasks;

namespace KnightBus.Core.Sagas
{
    public interface ISaga
    {
        ISagaMessageMapper MessageMapper { get; }
    }
    public interface ISaga<T> : ISaga where T : ISagaData
    {
        /// <summary>
        /// Stateful data associated with the Saga
        /// </summary>
        T Data { get; set; }

        ISagaStore SagaStore { get; set; }
        Task CompleteAsync();
        Task UpdateAsync(T sagaData);
        Task FailAsync();
    }

    public abstract class Saga<T> : ISaga<T> where T : ISagaData
    {
        public T Data { get; set; }
        public ISagaMessageMapper MessageMapper { get; } = new SagaMessageMapper();
        public ISagaStore SagaStore { get; set; }
        public virtual Task CompleteAsync()
        {
            return SagaStore.Complete(Data.Id, Data.Key);
        }

        public virtual Task UpdateAsync(T sagaData)
        {
            return SagaStore.Update(Data.Id, sagaData);
        }

        public virtual Task FailAsync()
        {
            return SagaStore.Fail(Data.Key, Data.Id);
        }
    }

    public interface ISagaData
    {
        string Id { get; }
        string Key { get; }
    }

    public interface ISagaStore 
    {
        Task<T> GetSaga<T>(string id, string key);
        Task<T> Create<T>(string id, T sagaData);
        Task Update<T>(string id, T sagaData);
        Task Complete(string key, string id);
        Task Fail(string key, string id);
    }

    public class SagaAlreadyStartedException : Exception { }
    public class SagaNotFoundStartedException : Exception { }
}
