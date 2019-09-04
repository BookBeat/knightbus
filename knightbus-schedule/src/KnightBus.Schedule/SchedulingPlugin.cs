using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using Quartz;
using Quartz.Impl;


namespace KnightBus.Schedule
{
    public class SchedulingPlugin : IPlugin
    {
        private readonly IHostConfiguration _configuration;
        private IScheduler _scheduler;

        public SchedulingPlugin(IHostConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ConsoleWriter.WriteLine($"Starting {nameof(SchedulingPlugin)}");
            var lockManager = _configuration.SingletonLockManager;
            var dependencyInjection = _configuration.DependencyInjection;
            var log = _configuration.Log;

            await lockManager.InitializeAsync().ConfigureAwait(false);
            var triggerProcessors = dependencyInjection.GetOpenGenericRegistrations(typeof(IProcessSchedule<>)).ToList();
            var schedulerFactory = new StdSchedulerFactory();
            _scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
            var jobFactory = new CustomJobFactory();
            _scheduler.JobFactory = jobFactory;
            await _scheduler.Start(cancellationToken).ConfigureAwait(false);

            foreach (var processor in triggerProcessors)
            {
                var processorInterfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor, typeof(IProcessSchedule<>));
                foreach (var processorInterface in processorInterfaces)
                {
                    var settingsType = processorInterface.GenericTypeArguments[0];
                    ConsoleWriter.WriteLine($"Found {processor.Name}<{settingsType.Name}>");
                    var settings = (ISchedule)Activator.CreateInstance(settingsType);
                    

                    CronExpression.ValidateExpression(settings.CronExpression);

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
                    
                    jobFactory.AddJob(settingsType, dependencyInjection, log, lockManager);
                    await _scheduler.ScheduleJob(job, trigger, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
