using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using Quartz;
using Quartz.Impl;


namespace KnightBus.Schedule
{
    public class KnightWatchHost
    {
        private readonly ISingletonLockManager _lockManager;
        private readonly IDependencyInjection _dependencyInjection;
        private readonly ILog _log;
        private IScheduler _scheduler;

        public KnightWatchHost(ISingletonLockManager lockManager, IDependencyInjection dependencyInjection, ILog log)
        {
            _lockManager = lockManager;
            _dependencyInjection = dependencyInjection;
            _log = log;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _lockManager.InitializeAsync().ConfigureAwait(false);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var triggerProcessors = ReflectionHelper.GetAllTypesImplementingOpenGenericInterface(typeof(IProcessTrigger<>), assemblies).ToList();
            var schedulerFactory = new StdSchedulerFactory();
            _scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
            var jobFactory = new CustomJobFactory();
            _scheduler.JobFactory = jobFactory;
            await _scheduler.Start(cancellationToken).ConfigureAwait(false);

            foreach (var processor in triggerProcessors)
            {
                Console.WriteLine($"Found trigger processor {processor.Name}");
                var processorInterfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor, typeof(IProcessTrigger<>));
                foreach (var processorInterface in processorInterfaces)
                {
                    var settingsType = processorInterface.GenericTypeArguments[0];
                    var settings = (ITriggerSettings)Activator.CreateInstance(settingsType);
                    var jobType = typeof(JobExecutor<>).MakeGenericType(settingsType);
                    var job = JobBuilder.Create(jobType)
                        .WithIdentity(Guid.NewGuid().ToString())
                        .Build();

                    var trigger = TriggerBuilder.Create()
                        .ForJob(job)
                        .WithCronSchedule(settings.CronExpression)
                        .WithIdentity(Guid.NewGuid().ToString())
                        .StartNow()
                        .Build();


                    jobFactory.AddJob(settingsType, _dependencyInjection, _log, _lockManager);
                    await _scheduler.ScheduleJob(job, trigger, cancellationToken).ConfigureAwait(false);    
                }
            }
        }
    }
}
