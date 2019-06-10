using System;
using System.Threading.Tasks;

namespace KnightBus.Core.Sagas
{
    public interface ISaga
    {
        string Id { get; }
        string InstanceId { get; set; }
        ISagaMessageMapper MessageMapper { get; }
    }
    public interface ISaga<T> : ISaga
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

    public abstract class Saga<T> : ISaga<T>
    {
        public abstract string Id { get; }
        public string InstanceId { get; set; }
        public T Data { get; set; }
        public ISagaMessageMapper MessageMapper { get; } = new SagaMessageMapper();
        public ISagaStore SagaStore { get; set; }
        public virtual Task CompleteAsync()
        {
            return SagaStore.Complete(Id, InstanceId);
        }

        public virtual Task UpdateAsync(T sagaData)
        {
            return SagaStore.Update(Id, InstanceId, sagaData);
        }

        public virtual Task FailAsync()
        {
            return SagaStore.Fail(Id, InstanceId);
        }
    }


    public class SagaAlreadyStartedException : Exception { }
    public class SagaNotFoundStartedException : Exception { }
}
