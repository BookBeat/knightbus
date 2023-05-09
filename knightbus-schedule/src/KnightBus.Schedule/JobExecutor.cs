using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;
using Quartz;

namespace KnightBus.Schedule
{
    internal class JobExecutor<T> : IJob where T : class, ISchedule, new()
    {
        private readonly IDependencyInjection _dependencyInjection;
        private readonly ISingletonLockManager _lockManager;

        private readonly ILogger _logger;

        public JobExecutor(ILogger logger, ISingletonLockManager lockManager, IDependencyInjection dependencyInjection)
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

                _logger.LogInformation("Executing schedule {Schedule}", schedule);

                using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken))
                {
                    using (new SingletonTimerScope(_logger, lockHandle, false, TimeSpan.FromSeconds(19), linkedTokenSource))
                    using (var scopedDependencyInjection = _dependencyInjection.GetScope())
                    {
                        var processor = scopedDependencyInjection.GetInstance<IProcessSchedule<T>>();
                        await processor.ProcessAsync(linkedTokenSource.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing schedule {Schedule}", typeof(T));
            }
        }
    }
}
