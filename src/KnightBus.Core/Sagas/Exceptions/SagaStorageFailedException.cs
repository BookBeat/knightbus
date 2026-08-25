namespace KnightBus.Core.Sagas.Exceptions;

public class SagaStorageFailedException : SagaException
{
    public SagaStorageFailedException(string partitionKey, string id)
        : base(partitionKey, id) { }
}
