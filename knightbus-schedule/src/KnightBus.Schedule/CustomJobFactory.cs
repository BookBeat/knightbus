using System;
using System.Collections.Concurrent;
using System.Reflection;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Quartz;
using Quartz.Simpl;
using Quartz.Spi;

namespace KnightBus.Schedule
{
    public class CustomJobFactory : SimpleJobFactory
    {
        private readonly ConcurrentDictionary<Type, IJob> _jobs = new ConcurrentDictionary<Type, IJob>();

        public CustomJobFactory(Assembly[] assemblies, IDependencyInjection dependencyInjection, ILog log, ISingletonLockManager singletonLockManager)
        {
            var types = ReflectionHelper.GetAllTypesImplementingInterface(typeof(ITriggerSettings), assemblies);

            foreach (var type in types)
            {
                var jobType = typeof(JobExecutor<>).MakeGenericType(type);
                var jobExecutor = (IJob)Activator.CreateInstance(jobType, log, singletonLockManager, dependencyInjection);
                _jobs.TryAdd(jobExecutor.GetType(), jobExecutor);
            }
        }

        public override IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            return _jobs[bundle.JobDetail.JobType];
        }
    }
}