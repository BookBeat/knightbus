using System;

namespace KnightBus.Host;

internal class TransportMissingException : Exception
{
    public TransportMissingException(Type messageType)
        : base($"No transport found for {messageType}, did you forget to register it?") { }

    public TransportMissingException(string message)
        : base(message) { }
}
