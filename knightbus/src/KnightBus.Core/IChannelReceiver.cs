using System.Threading.Tasks;

namespace KnightBus.Core
{
    public interface IChannelReceiver
    {
        Task StartAsync();
        IProcessingSettings Settings { get; set; }
    }
}