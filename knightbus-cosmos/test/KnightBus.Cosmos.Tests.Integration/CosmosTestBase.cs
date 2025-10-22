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
    public ICosmosBus? Publisher;
    public string? _databaseId;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        Console.WriteLine("Starting CosmosDB example");

        _databaseId = "KnightBus";

        //Connection string should be saved as environment variable named "CosmosString"
        var connectionString = Environment.GetEnvironmentVariable("CosmosString");

        //Setup database
        CosmosClient setupClient = new CosmosClient(connectionString);
        await setupClient.CreateDatabaseIfNotExistsAsync(
            id: "KnightBus",
            throughputProperties: ThroughputProperties.CreateAutoscaleThroughput(1000)
        );
        setupClient.Dispose();

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
                        configuration.PollingDelay = TimeSpan.FromMilliseconds(500);
                        configuration.DefaultTimeToLive = TimeSpan.FromSeconds(120);
                        configuration.ClientOptions = new CosmosClientOptions()
                        {
                            AllowBulkExecution = true,
                            MaxRetryAttemptsOnRateLimitedRequests = 200,
                            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60),
                        };
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

        Publisher = knightBusHost
            .Services.CreateScope()
            .ServiceProvider.GetRequiredService<ICosmosBus>();
    }
}
