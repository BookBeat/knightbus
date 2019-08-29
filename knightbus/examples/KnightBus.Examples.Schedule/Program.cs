using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage.Singleton;
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
            container.Register(typeof(IProcessTrigger<>), new List<Assembly>{Assembly.GetExecutingAssembly()});
            var dependency = new SimpleInjectorDependencyInjection(container);
            ISingletonLockManager singletonLockManager = new BlobLockManager("");
            singletonLockManager.InitializeAsync().GetAwaiter().GetResult();
            container.Verify();

            var host = new KnightWatchHost(singletonLockManager, dependency, new NoLogging());

            host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            Console.ReadLine();
        }
        static async Task MainAsync(string[] args)
        {
            var container = new Container();
            container.Register(typeof(IProcessTrigger<>), new List<Assembly>{Assembly.GetExecutingAssembly()});
            var dependency = new SimpleInjectorDependencyInjection(container);
            ISingletonLockManager singletonLockManager = new BlobLockManager("");
            await singletonLockManager.InitializeAsync();
            container.Verify();

            var host = new KnightWatchHost(singletonLockManager, dependency, new NoLogging());

            await host.StartAsync(CancellationToken.None);

            Console.ReadLine();
        }
    }

    public class EveryMinute : ITriggerSettings
    {
        public string CronExpression => "0 * * ? * *";
    }

    public class MyTrigger : IProcessTrigger<EveryMinute>
    {
        public Task ProcessAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Yo!");
            return Task.CompletedTask;
        }
    }
}
