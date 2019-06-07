using System;
using System.Threading.Tasks;

namespace KnightBus.Core.Sagas
{
    public interface ISaga
    {

    }
    public interface ISaga<T> : ISaga where T : ISagaData
    {
        /// <summary>
        /// Stateful data associated with the Saga
        /// </summary>
        T Data { get; set; }
        ISagaMessageMapper MessageMapper { get; }
        ISagaStore SagaStore { get; set; }
    }

    public class Saga<T> : ISaga<T> where T : ISagaData
    {

        public T Data { get; set; }
        public ISagaMessageMapper MessageMapper { get; } = new SagaMessageMapper();
        public ISagaStore SagaStore { get; set; }
    }

    public interface ISagaData
    {
        string Key { get; }
    }

    public interface ISagaStore 
    {
        Task<T> GetSaga<T>(string id);
        Task<T> Create<T>(string id, T sagaData);
        Task Update<T>(T saga);
        Task Complete(string id);
        Task Fail(string id);
    }

    public class SagaAlreadyStartedException : Exception { }
    public class SagaNotFoundStartedException : Exception { }
}
