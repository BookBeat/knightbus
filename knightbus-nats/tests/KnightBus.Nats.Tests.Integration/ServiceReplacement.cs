namespace KnightBus.Nats.Tests.Integration;

public class ServiceReplacement<T> where T : class
{
    public T Service { get; private set; }

    public void Replace(T service)
    {
        Service = service;
    }
}
