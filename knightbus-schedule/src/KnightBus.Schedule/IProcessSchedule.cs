using System.Threading;
using System.Threading.Tasks;

namespace KnightBus.Schedule
{
    public interface IProcessSchedule<T> where T : class, ISchedule, new()
    {
        Task ProcessAsync(CancellationToken cancellationToken);
    }
}