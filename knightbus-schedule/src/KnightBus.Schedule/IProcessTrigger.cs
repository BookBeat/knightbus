using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Schedule
{
    public interface IProcessTrigger<T> where T : class, ITriggerSettings, new()
    {
        Task ProcessAsync(CancellationToken cancellationToken);
    }
}