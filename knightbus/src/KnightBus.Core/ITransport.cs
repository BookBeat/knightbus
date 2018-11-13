namespace KnightBus.Core
{
    /// <summary>
    /// Defines a message transport
    /// </summary>
    public interface ITransport
    {
        ITransportChannelFactory[] TransportChannelFactories { get; }
        ITransport ConfigureChannels(ITransportConfiguration configuration);
        ITransport UseMiddleware(IMessageProcessorMiddleware middleware);
    }
}