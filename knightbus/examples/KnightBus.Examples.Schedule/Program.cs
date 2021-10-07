using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage;
using KnightBus.Host;
using KnightBus.Schedule;
using KnightBus.SimpleInjector;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace KnightBus.Examples.Schedule
{
    public class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }
        static async Task MainAsync(string[] args)
        {
            var connectionString = "";
            var container = new Container {Options = { DefaultScopedLifestyle = new AsyncScopedLifestyle()}};

            var host = new KnightBusHost()
                .Configure(conf => 
                    conf.UseScheduling()
                        .UseSimpleInjector(container)
                        .RegisterSchedules(Assembly.GetExecutingAssembly())
                        .UseBlobStorageLockManager(connectionString)
                    );
            container.Verify();
            await host.StartAndBlockAsync(CancellationToken.None);
        }
    }

    public class EveryMinute : ISchedule
    {
        public string CronExpression => "0 * * ? * *";
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
    }
    public class EveryMinuteToo : ISchedule
    {
        public string CronExpression => "0 * * ? * *";
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
    }

    public class MySchedule : IProcessSchedule<EveryMinute>, IProcessSchedule<EveryMinuteToo>
    {
        public Task ProcessAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Yo!");
            return Task.CompletedTask;
        }
    }
}
