using System;
using System.Threading;
using System.Threading.Tasks;
using KnightBus.Azure.Storage;
using KnightBus.Host;
using KnightBus.Schedule;
using Microsoft.Extensions.Hosting;
using Testcontainers.Azurite;

namespace KnightBus.Examples.Schedule;

public class Program
{
    private static readonly AzuriteContainer Azurite = new AzuriteBuilder().WithCommand("--skipApiVersionCheck").Build();
    static void Main(string[] args)
    {
        MainAsync(args).GetAwaiter().GetResult();
    }
    static async Task MainAsync(string[] args)
    {
        await Azurite.StartAsync();
        var blobConnection = Azurite.GetConnectionString();

        var knightBus = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices(services =>
            {
                services.UseBlobStorage(blobConnection)
                .UseScheduling()
                .UseTcpAliveListener(13000)
                .RegisterSchedules()
                .UseBlobStorageLockManager();
            })
            .UseKnightBus().Build();


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
        Console.WriteLine("MySchedule: Schedule triggered!");
        return Task.CompletedTask;
    }
}

public class AnotherSchedule : IProcessSchedule<EveryMinute>
{
    public Task ProcessAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("AnotherSchedule: Schedule triggered!");
        return Task.CompletedTask;
    }
}
