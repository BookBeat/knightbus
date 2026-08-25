using System;

namespace KnightBus.Core.Sagas.Exceptions;

public class SagaException : Exception
{
    public SagaException(string partitionKey, string id)
        : base($"Saga with partition {partitionKey} and id {id}") { }
}
