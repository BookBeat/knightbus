using System.Collections.Concurrent;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Cosmos.Messages;
using KnightBus.Host;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Cosmos.Tests.Integration;

public abstract class CosmosTestBase
{
    public CosmosBus _publisher;
    public string? _databaseId;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        Console.WriteLine("Starting CosmosDB example");

        //Connection string should be saved as environment variable named "CosmosString"
        var connectionString = Environment.GetEnvironmentVariable("CosmosString");
        _databaseId = "PubSub";
        const string leaseContainer = "Leases";

        var knightBusHost = Microsoft
            .Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices(services =>
            {
                services
                    .UseCosmos(configuration =>
                    {
                        configuration.ConnectionString = connectionString;
                        configuration.Database = _databaseId;
                        configuration.LeaseContainer = leaseContainer;
                        configuration.PollingDelay = TimeSpan.FromMilliseconds(500);
                        configuration.DefaultTimeToLive = TimeSpan.FromSeconds(120);
                    })
                    .RegisterProcessors(typeof(ProcessingTests).Assembly) //Can be any class name in this project
                    .UseTransport<CosmosTransport>();
                services.AddSingleton<ProcessedTracker>();
            })
            .UseKnightBus()
            .Build();

        //Start the KnightBus Host
        await knightBusHost.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(5));

        _publisher = knightBusHost
            .Services.CreateScope()
            .ServiceProvider.GetRequiredService<CosmosBus>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _publisher.Dispose();
    }
}
