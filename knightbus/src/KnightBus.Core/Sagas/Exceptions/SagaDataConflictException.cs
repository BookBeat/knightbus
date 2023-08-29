namespace KnightBus.Core.Sagas.Exceptions;

public class SagaDataConflictException : SagaException
{
    public SagaDataConflictException(string partitionKey, string id) : base(partitionKey, id)
    {
    }
}
