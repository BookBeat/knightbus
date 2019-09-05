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

        private readonly ILog _logger;
        private readonly ISingletonLockManager _lockManager;
        private readonly IDependencyInjection _dependencyInjection;

        public JobExecutor(ILog logger, ISingletonLockManager lockManager, IDependencyInjection dependencyInjection)
        {

            _logger = logger;
            _lockManager = lockManager;
            _dependencyInjection = dependencyInjection;
        }

        async Task IJob.Execute(IJobExecutionContext context)
        {
            try
            {
                var schedule = typeof(T).FullName;
                var lockHandle = await _lockManager.TryLockAsync(schedule, TimeSpan.FromSeconds(60), CancellationToken.None).ConfigureAwait(false);

                if (lockHandle == null)
                {
                    //someone else has locked this instance, do nothing
                    return;
                }

                _logger.Information("Executing schedule {Schedule} {LockHandle}", schedule, lockHandle);

                using (new SingletonTimerScope(_logger, lockHandle, false))
                {
                    using (_dependencyInjection.GetScope())
                    {
                        var processor = _dependencyInjection.GetInstance<IProcessSchedule<T>>();
                        await processor.ProcessAsync(context.CancellationToken).ConfigureAwait(false);
                    }
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