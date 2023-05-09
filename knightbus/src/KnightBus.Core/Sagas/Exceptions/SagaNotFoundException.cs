namespace KnightBus.Core.Sagas.Exceptions
{
    public class SagaNotFoundException : SagaException
    {
        public SagaNotFoundException(string partitionKey, string id) : base(partitionKey, id)
        {
        }
    }
}
