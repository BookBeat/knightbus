using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Exceptions;
using KnightBus.Core.Singleton;
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
            var lockManager = _configuration.DependencyInjection.GetInstance<ISingletonLockManager>();
            var dependencyInjection = _configuration.DependencyInjection;
            var log = _configuration.Log;

            await lockManager.InitializeAsync().ConfigureAwait(false);
            var scheduleProcessors = dependencyInjection.GetOpenGenericRegistrations(typeof(IProcessSchedule<>)).ToList();
            var schedulerFactory = new StdSchedulerFactory();
            _scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
            var jobFactory = new CustomJobFactory();
            _scheduler.JobFactory = jobFactory;
            await _scheduler.Start(cancellationToken).ConfigureAwait(false);

            foreach (var processor in scheduleProcessors)
            {
                var processorInterfaces = ReflectionHelper.GetAllInterfacesImplementingOpenGenericInterface(processor, typeof(IProcessSchedule<>));
                foreach (var processorInterface in processorInterfaces)
                {
                    var scheduleType = processorInterface.GenericTypeArguments[0];
                    ConsoleWriter.WriteLine($"Found {processor.Name}<{scheduleType.Name}>");
                    var settings = (ISchedule)Activator.CreateInstance(scheduleType);
                    

                    CronExpression.ValidateExpression(settings.CronExpression);

                    var jobType = typeof(JobExecutor<>).MakeGenericType(scheduleType);
                    var job = JobBuilder.Create(jobType)
                        .WithIdentity(Guid.NewGuid().ToString())
                        .Build();

                    var trigger = TriggerBuilder.Create()
                        .ForJob(job)
                        .WithCronSchedule(settings.CronExpression, builder=> builder.InTimeZone(settings.TimeZone))
                        .WithIdentity(Guid.NewGuid().ToString())
                        .StartNow()
                        .Build();
                    
                    jobFactory.AddJob(scheduleType, dependencyInjection, log, lockManager);
                    await _scheduler.ScheduleJob(job, trigger, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
