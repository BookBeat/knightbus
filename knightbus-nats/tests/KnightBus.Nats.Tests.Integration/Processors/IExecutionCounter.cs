namespace KnightBus.Nats.Tests.Integration.Processors;

public interface IExecutionCounter
{
    public void Increment();
    int Count { get; }
}
