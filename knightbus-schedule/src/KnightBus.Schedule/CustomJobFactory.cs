using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Quartz;
using Quartz.Simpl;
using Quartz.Spi;

[assembly: InternalsVisibleTo("KnightBus.Schedule.Tests.Unit")]
namespace KnightBus.Schedule
{
    public class CustomJobFactory : SimpleJobFactory
    {
        private readonly ConcurrentDictionary<Type, IJob> _jobs = new ConcurrentDictionary<Type, IJob>();

        internal void AddJob(Type settingsType, IDependencyInjection dependencyInjection, ILog log, ISingletonLockManager singletonLockManager)
        {
            var jobType = typeof(JobExecutor<>).MakeGenericType(settingsType);
            var jobExecutor = (IJob)Activator.CreateInstance(jobType, log, singletonLockManager, dependencyInjection);
            _jobs.TryAdd(jobExecutor.GetType(), jobExecutor);
        }

        public override IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            return _jobs[bundle.JobDetail.JobType];
        }
    }
}