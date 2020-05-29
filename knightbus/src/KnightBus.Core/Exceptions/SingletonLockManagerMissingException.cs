namespace KnightBus.Core.Exceptions
{
    public sealed class SingletonLockManagerMissingException : KnightBusException
    {
        public SingletonLockManagerMissingException(string message = null) : base(message ?? "No ISingletonLockManager found, did you forget to configure it?")
        { }
    }
}