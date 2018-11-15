using System.Threading.Tasks;

namespace KnightBus.Core
{
    /// <summary>
    /// Receives messages from a channel and forwards them into the processing pipeline
    /// </summary>
    public interface IChannelReceiver
    {
        Task StartAsync();
        IProcessingSettings Settings { get; set; }
    }
}