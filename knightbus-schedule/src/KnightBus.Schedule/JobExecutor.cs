using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Quartz;

namespace KnightBus.Schedule
{
    internal class JobExecutor<T> : IJob where T : class, ITriggerSettings, new()
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
            //Try and get the lock
            var lockId = typeof(T).FullName;
            var lockHandle = await _lockManager.TryLockAsync(lockId, TimeSpan.FromSeconds(60), CancellationToken.None).ConfigureAwait(false);

            if (lockHandle == null)
            {
                //someone else has locked this instance, do nothing
                return;
            }
            _logger.Information("Executing trigger {TriggerSetting} {LockHandle}", lockId, lockHandle);

            using (new SingletonTimerScope(_logger, lockHandle, false))
            {
                using (_dependencyInjection.GetScope())
                {
                    try
                    {
                        var processor = _dependencyInjection.GetInstance<IProcessTrigger<T>>();
                        await processor.ProcessAsync(context.CancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error processing trigger {typeof(T)} {e.Message} {e.StackTrace}");
                        _logger.Error(e, "Error processing trigger {Trigger}", typeof(T));
                    }
                }
            }
        }
    }
}