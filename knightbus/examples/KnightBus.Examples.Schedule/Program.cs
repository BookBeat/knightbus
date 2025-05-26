using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage;
using KnightBus.Host;
using KnightBus.Schedule;
using Microsoft.Extensions.Hosting;

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

        var knightBus = global::Microsoft
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
                    .UseBlobStorageLockManager();
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
