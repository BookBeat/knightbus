namespace KnightBus.Core
{
    public interface ITransportConfiguration
    {
        string ConnectionString { get; }
        IMessageSerializer MessageSerializer { get; set; }
    }
}