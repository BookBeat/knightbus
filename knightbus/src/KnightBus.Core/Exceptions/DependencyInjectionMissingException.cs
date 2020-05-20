namespace KnightBus.Core.Exceptions
{
    public sealed class DependencyInjectionMissingException : KnightBusException
    {
        public DependencyInjectionMissingException() : base("No dependency injection found")
        {}
    }
}