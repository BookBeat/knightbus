namespace KnightBus.Messages
{
    public interface IEventSubscription<T> where T : IEvent
    {
        string Name { get; }
    }
}