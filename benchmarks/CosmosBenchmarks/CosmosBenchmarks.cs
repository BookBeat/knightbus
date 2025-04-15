using System;
using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System.Collections.Concurrent;
using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Cosmos;
using KnightBus.Cosmos.Messages;
using KnightBus.Host;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cosmos.Benchmarks
class ProcessedMessages()
{
    public static ConcurrentDictionary<string, int> dict { get; set; } = new ConcurrentDictionary<string, int>();

    public static void Increment(string key)
    {
        dict.AddOrUpdate(key, 1, (_, val) => val + 1);
    }
    public static bool MostMessagesInDictionary(IEnumerable<string> messageStrings, double threshhold = 0, int deliveriesPerMessage = 1)
    {
        if(!messageStrings.Any())
            throw new ArgumentException("List not correctly provided (can not be empty)");
        
        int messageCount = messageStrings.Count();
        int failedMessages = 0;
        foreach (var id in messageStrings)
        {
            if (!dict.TryGetValue(id, out var value) || value < deliveriesPerMessage)
            {
                failedMessages++;
            }
        }

        return failedMessages < messageCount * threshhold;
    }

    public static void DuplicatesAndMissed(IEnumerable<string> messageStrings, int deliveriesPerMessage = 1)
    {
        int notProcessed = 0;
        int duplicateProcessed = 0;
        foreach (var id in messageStrings)
        {
            if (!dict.TryGetValue(id, out var value) || value < deliveriesPerMessage)
            {
                notProcessed++;
            }
            else if(value > deliveriesPerMessage)
            {
                duplicateProcessed++;
            }
        }
        Console.WriteLine($"Not Processed: {notProcessed}");
        Console.WriteLine($"Duplicate Processed: {duplicateProcessed}");
    }
}

class CosmosBenchmarks
{
    private CosmosBus _publisher;
    private string? _databaseId;

    [GlobalSetup]
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
                    .RegisterProcessors(typeof(CosmosBenchmarks).Assembly) //Can be any class name in this project
                    .UseTransport<CosmosTransport>();

                services.AddSingleton<ProcessedMessages>();
            })
            .UseKnightBus()
            .Build();

        //Start the KnightBus Host
        await knightBusHost.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(1));

        _publisher = knightBusHost.Services.CreateScope().ServiceProvider.GetRequiredService<CosmosBus>();
    }

    [GlobalCleanup]
    public void OneTimeTearDown()
    {
        _publisher.CleanUp();
    }

    [IterationCleanup]
    public void TearDown()
    {
        ProcessedMessages.dict = new ConcurrentDictionary<string, int>();
    }

    [Benchmark]
    public async Task AllCommandsProcessed()
    {
        const int numMessages = 3000; // 65s / 10000
        //Send some commands
        SampleCosmosCommand[] messages = new SampleCosmosCommand[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new SampleCosmosCommand() { MessageBody = $"msg data {i}" };
            messageContents[i] = messages[i].MessageBody;
        }
        
        var startTime = DateTime.UtcNow;
        await _publisher.SendAsync(messages, CancellationToken.None);
        
        while (!ProcessedMessages.AllMessagesInDictionary(messageContents) && (DateTime.UtcNow.Subtract(startTime)) < TimeSpan.FromMinutes(4))
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Console.WriteLine($"Elapsed time: {DateTime.UtcNow.Subtract(startTime).TotalSeconds} seconds");
        ProcessedMessages.DuplicatesAndMissed(messageContents);
    }
    
    
    [Benchmark]
    public async Task AllEventsProcessedWhenOneSubscriber()
    {
        const int numMessages = 1000;
        OneSubCosmosEvent[] messages = new OneSubCosmosEvent[numMessages];
        string[] messageContents = new string[numMessages];
        
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new OneSubCosmosEvent() { MessageBody = $"msg data {i}" };
            messageContents[i] = messages[i].MessageBody;
        }
        var startTime = DateTime.UtcNow;
        await _publisher.PublishAsync(messages, CancellationToken.None);
        
        while (!ProcessedMessages.AllMessagesInDictionary(messageContents) && DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        Console.WriteLine($"Elapsed time: {DateTime.UtcNow.Subtract(startTime).TotalSeconds} seconds");
    }
    
    [Benchmark]
    public async Task AllEventsProcessedWhenToTwoSubscribers()
    {
        const int numMessages = 100;
        for (int i = 0; i < numMessages; i++)
        {
            await _publisher.PublishAsync(new TwoSubCosmosEvent() { MessageBody = $"data {i}" }, CancellationToken.None);
        }
        
        TwoSubCosmosEvent[] messages = new TwoSubCosmosEvent[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new TwoSubCosmosEvent() { MessageBody = $"msg data {i}" };
            messageContents[i] = messages[i].MessageBody;
        }
        await _publisher.PublishAsync(messages, CancellationToken.None);
        
        
        var startTime = DateTime.UtcNow;
        while (!ProcessedMessages.AllMessagesInDictionary(messageContents,2) && DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    [Benchmark]
    public async Task FailedEventsShouldBeRetriedSetNumberOfTimes()
    {
        const int numMessages = 10;
        for (int i = 0; i < numMessages; i++)
        {
            await _publisher.PublishAsync(new PoisonEvent() { Body = $"data {i}" }, CancellationToken.None);
        }
        
        PoisonEvent[] messages = new PoisonEvent[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new PoisonEvent() { Body = $"msg data {i}" };
            messageContents[i] = messages[i].Body;
        }
        await _publisher.PublishAsync(messages, CancellationToken.None);
        
        var startTime = DateTime.UtcNow;
        int deadLetterLimit = new CosmosProcessingSetting().DeadLetterDeliveryLimit;
        while (!ProcessedMessages.AllMessagesInDictionary(messageContents,deadLetterLimit) && DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}
