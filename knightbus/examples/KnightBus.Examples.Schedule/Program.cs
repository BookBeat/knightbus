using System;
using System.Threading.Tasks;
using KnightBus.Core;
using KnightBus.Core.Singleton;
using KnightBus.Schedule;
using KnightBus.SimpleInjector;
using SimpleInjector;

namespace KnightBus.Examples.Schedule
{
    public class Program
    {
        static void Main(string[] args)
        {
            var container = new Container();
            var dependency = new SimpleInjectorDependencyInjection(container);
            ISingletonLockManager singletonLockManager = null;

            var host = new KnightWatchHost(singletonLockManager, dependency, new NoLogging());

            host.Start();

            Console.ReadLine();
        }
    }

    public class EveryMinute : ITriggerSettings
    {
        public string CronExpression => "0 * * ? * *";
    }

    public class MyTrigger : IProcessTrigger<EveryMinute>
    {
        public Task ProcessAsync()
        {
            Console.WriteLine("Yo!");
            return Task.CompletedTask;
        }
    }
}
