namespace KnightBus.Core.Exceptions
{
    public sealed class DependencyInjectionNotBuiltException : KnightBusException
    {
        public DependencyInjectionNotBuiltException() : base("Dependency injection was not built. Did you forget to call Build()?")
        {}
    }
}