using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage;
using KnightBus.Core.Singleton;
using KnightBus.Host;
using KnightBus.Schedule;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnightBus.Examples.Schedule;

public class Program
{
    static void Main(string[] args)
    {
        MainAsync(args).GetAwaiter().GetResult();
    }

    static async Task MainAsync(string[] args)
    {
        var blobConnection = "UseDevelopmentStorage=true";

        var knightBus = Microsoft
            .Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices(services =>
            {
                services
                    .UseBlobStorage(blobConnection)
                    .UseScheduling()
                    .UseTcpAliveListener(13000)
                    .RegisterSchedules()
                    .AddSingleton<ISingletonLockManager, InMemoryLockManager>();
                // .UseBlobStorageLockManager();
            })
            .UseKnightBus()
            .Build();

        await knightBus.RunAsync(CancellationToken.None);
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
        Console.WriteLine("Schedule triggered!");
        return Task.CompletedTask;
    }
}

public class InMemoryLockManager : ISingletonLockManager
{
    private readonly ConcurrentDictionary<string, int> _heldLocks = new();

    public Task<ISingletonLockHandle> TryLockAsync(
        string lockId,
        TimeSpan lockPeriod,
        CancellationToken cancellationToken
    )
    {
        if (_heldLocks.TryAdd(lockId, 0))
        {
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(50));
                _heldLocks.TryRemove(lockId, out _);
            });
            return Task.FromResult<ISingletonLockHandle>(new InMemoryLockHandle(lockId));
        }

        return Task.FromResult<ISingletonLockHandle>(null);
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}

public class InMemoryLockHandle : ISingletonLockHandle
{
    public string LeaseId => LockId;
    public string LockId { get; }

    public InMemoryLockHandle(string lockId)
    {
        LockId = lockId;
    }

    public Task<bool> RenewAsync(ILogger log, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task ReleaseAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
