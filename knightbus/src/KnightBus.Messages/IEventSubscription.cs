namespace KnightBus.Messages
{
    public interface IEventSubscription
    {
        string Name { get; }
    }
    public interface IEventSubscription<T> : IEventSubscription where T : IEvent
    { }
}
