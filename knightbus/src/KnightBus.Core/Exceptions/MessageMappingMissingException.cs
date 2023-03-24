namespace KnightBus.Core.Exceptions
{
    public sealed class MessageMappingMissingException : KnightBusException
    {
        public MessageMappingMissingException(string message) : base(message)
        { }
    }
}
