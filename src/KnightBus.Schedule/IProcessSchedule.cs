using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;

namespace KnightBus.Schedule;

public interface IProcessSchedule<T> : IGenericProcessor
    where T : class, ISchedule, new()
{
    Task ProcessAsync(CancellationToken cancellationToken);
}
