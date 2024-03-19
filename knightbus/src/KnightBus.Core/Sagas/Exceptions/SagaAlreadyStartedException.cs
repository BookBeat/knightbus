namespace KnightBus.Core.Sagas.Exceptions;

public class SagaAlreadyStartedException : SagaException
{
    public SagaAlreadyStartedException(string partitionKey, string id) : base(partitionKey, id)
    {
    }
}
