using System;

namespace KnightBus.Core.Exceptions;

public abstract class KnightBusException : Exception
{
    protected KnightBusException(string message)
        : base(message) { }

    protected KnightBusException(string message, Exception inner)
        : base(message, inner) { }
}
