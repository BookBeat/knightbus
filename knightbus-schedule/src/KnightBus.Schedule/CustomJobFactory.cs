using System;
using System.Collections.Concurrent;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Simpl;
using Quartz.Spi;

namespace KnightBus.Schedule;

public class CustomJobFactory : SimpleJobFactory
{
    private readonly ConcurrentDictionary<Type, IJob> _jobs =
        new ConcurrentDictionary<Type, IJob>();

    internal void AddJob(
        Type settingsType,
        Type processorType,
        IDependencyInjection dependencyInjection,
        ILogger log,
        ISingletonLockManager singletonLockManager
    )
    {
        var jobType = typeof(JobExecutor<,>).MakeGenericType(settingsType, processorType);
        var jobExecutor = (IJob)
            Activator.CreateInstance(jobType, log, singletonLockManager, dependencyInjection);
        _jobs.TryAdd(jobExecutor.GetType(), jobExecutor);
    }

    public override IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        return _jobs[bundle.JobDetail.JobType];
    }
}
