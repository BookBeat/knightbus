namespace KnightBus.Core
{
    /// <summary>
    /// Defines a message transport
    /// </summary>
    public interface ITransport
    {
        ITransportFactory[] TransportFactories { get; }
        ITransportConfiguration Configuration { get; }
    }
}