using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Quartz;

namespace KnightBus.Schedule
{
    internal class JobExecutor<T> : IJob where T : class, ISchedule, new()
    {
        private readonly IDependencyInjection _dependencyInjection;
        private readonly ISingletonLockManager _lockManager;

        private readonly ILog _logger;

        public JobExecutor(ILog logger, ISingletonLockManager lockManager, IDependencyInjection dependencyInjection)
        {
            _logger = logger;
            _lockManager = lockManager;
            _dependencyInjection = dependencyInjection;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                var schedule = typeof(T).FullName;
                var lockHandle = await _lockManager
                    .TryLockAsync(schedule, TimeSpan.FromSeconds(60), CancellationToken.None).ConfigureAwait(false);

                if (lockHandle == null)
                    //someone else has locked this instance, do nothing
                    return;

                _logger.Information("Executing schedule {Schedule} {LockHandle}", schedule, lockHandle);

                var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                using (new SingletonTimerScope(_logger, lockHandle, false, linkedTokenSource))
                using (_dependencyInjection.GetScope())
                {
                    var processor = _dependencyInjection.GetInstance<IProcessSchedule<T>>();
                    await processor.ProcessAsync(linkedTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                ConsoleWriter.WriteLine($"Error processing schedule {typeof(T)} {e.Message} {e.StackTrace}");
                _logger.Error(e, "Error processing schedule {Schedule}", typeof(T));
            }
        }
    }
}