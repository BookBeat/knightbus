using System.Collections.Concurrent;
using FluentAssertions;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Host;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Cosmos.Tests.EndToEnd;

class ProcessedMessages()
{
    public static ConcurrentQueue<string> Queue { get; set; } = new ConcurrentQueue<string>();
    public static int Count()
    {
        return Queue.Count;
    }
}

[TestFixture]
class CosmosTests
{
    private CosmosBus _publisher;
    private CosmosClient _cosmosClient;
    private string? _databaseId;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        Console.WriteLine("Starting CosmosDB example");

        //Connection string should be saved as environment variable named "CosmosString"
        var connectionString = Environment.GetEnvironmentVariable("CosmosString");
        _databaseId = "PubSub";
        const string leaseContainer = "Leases";

        var knightBusHost = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureServices(services =>
            {
                services.UseCosmos(configuration =>
                    {
                        configuration.ConnectionString = connectionString;
                        configuration.Database = _databaseId;
                        configuration.LeaseContainer = leaseContainer;
                        configuration.PollingDelay = TimeSpan.FromMilliseconds(500);
                        configuration.DefaultTimeToLive = TimeSpan.FromSeconds(120);
                    })
                    .RegisterProcessors(typeof(CosmosTests).Assembly) //Can be any class name in this project
                    .UseTransport<CosmosTransport>();

                services.AddSingleton<ProcessedMessages>();
            })
            .UseKnightBus()
            .Build();

        //Start the KnightBus Host
        await knightBusHost.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(1));

        _publisher = knightBusHost.Services.CreateScope().ServiceProvider.GetRequiredService<CosmosBus>();

        _cosmosClient = new CosmosClient(connectionString);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _publisher.CleanUp();
    }

    [TearDown]
    public void TearDown()
    {
        ProcessedMessages.Queue = new ConcurrentQueue<string>();
    }

    [Test]
    public async Task AllCommandsProcessed()
    {
        const int numMessages = 100;
        //Send some commands
        SampleCosmosCommand[] messages = new SampleCosmosCommand[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new SampleCosmosCommand() { MessageBody = $"msg data {i}" };
        }
        await _publisher.SendAsync(messages, CancellationToken.None);


        await Task.Delay(TimeSpan.FromSeconds(10));
        
        ProcessedMessages.Count().Should().Be(numMessages);
    }
    
    [Test]
    public async Task AllEventsProcessedWhenOneSubscriber()
    {
        const int messages = 100;
        for (int i = 0; i < messages; i++)
        {
            await _publisher.PublishAsync(new TwoSubCosmosEvent() { MessageBody = $"data {i}" }, CancellationToken.None);
        }

        await Task.Delay(TimeSpan.FromSeconds(5));
        
        ProcessedMessages.Count().Should().Be(2*messages);
    }
    
    [Test]
    public async Task AllEventsProcessedWhenToTwoSubscribers()
    {
        const int messages = 100;
        for (int i = 0; i < messages; i++)
        {
            await _publisher.PublishAsync(new TwoSubCosmosEvent() { MessageBody = $"data {i}" }, CancellationToken.None);
        }

        await Task.Delay(TimeSpan.FromSeconds(5));
        
        ProcessedMessages.Count().Should().Be(2*messages);
    }

    [Test]
    public async Task FailedEventsShouldBeRetriedSetNumberOfTimes()
    {
        const int messages = 10;
        for (int i = 0; i < messages; i++)
        {
            await _publisher.PublishAsync(new PoisonEvent() { Body = $"data {i}" }, CancellationToken.None);
        }

        int failedAttemptsBeforeDeadLettering = new CosmosProcessingSetting().DeadLetterDeliveryLimit +1;
        await Task.Delay(TimeSpan.FromSeconds(5));
        ProcessedMessages.Count().Should().Be(messages * failedAttemptsBeforeDeadLettering);
    }
}
