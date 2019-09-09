using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Core
{
    public interface IPlugin
    {
        Task StartAsync(CancellationToken cancellationToken);
    }
}