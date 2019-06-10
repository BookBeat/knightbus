using System;
using System.Threading.Tasks;

namespace KnightBus.Core.Sagas
{
    public interface ISaga
    {
        string Id { get; }
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
        public abstract string Id { get; }
        public T Data { get; set; }
        public ISagaMessageMapper MessageMapper { get; } = new SagaMessageMapper();
        public ISagaStore SagaStore { get; set; }
        public virtual Task CompleteAsync()
        {
            return SagaStore.Complete(Id, Data.Id);
        }

        public virtual Task UpdateAsync(T sagaData)
        {
            return SagaStore.Update(Data.Id, sagaData);
        }

        public virtual Task FailAsync()
        {
            return SagaStore.Fail(Id, Data.Id);
        }
    }

    public interface ISagaData
    {
        /// <summary>
        /// The unique id for a specific saga
        /// </summary>
        string Id { get; }
    }

    public class SagaAlreadyStartedException : Exception { }
    public class SagaNotFoundStartedException : Exception { }
}
