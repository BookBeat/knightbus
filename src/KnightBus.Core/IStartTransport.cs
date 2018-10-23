using System.Threading.Tasks;

namespace KnightBus.Core
{
    public interface IStartTransport
    {
        Task StartAsync();
        IProcessingSettings Settings { get; set; }
        ITransportConfiguration Configuration { get; }
    }
}