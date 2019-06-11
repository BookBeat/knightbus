using System;

namespace KnightBus.Core.Sagas.Exceptions
{
    public class SagaAlreadyStartedException : Exception { }
    public class SagaStorageFailedException : Exception { }
}