using KnightBus.Messages;

namespace KnightBus.Core
{
    /// <summary>
    /// Transport specific configuration
    /// </summary>
    public interface ITransportConfiguration
    {
        string ConnectionString { get; set; }
        IMessageSerializer MessageSerializer { get; set; }
    }
}
