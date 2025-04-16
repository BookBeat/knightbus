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

namespace KnightBus.Cosmos.Tests.EndToEnd;

class ProcessedMessages()
{
    public static ConcurrentDictionary<string, int> dict { get; set; } = new ConcurrentDictionary<string, int>();
    public static Random Rng = new Random();
    
    public static void Increment(string key)
    {
        dict.AddOrUpdate(key, 1, (_, val) => val + 1);
    }
    public static bool AllMessagesInDictionary(IEnumerable<string> messageStrings, int deliveriesPerMessage = 1)
    {
        int notProcessed = 0;
        int duplicateProcessed = 0;
        
        foreach (var id in messageStrings)
        {
            if (!dict.TryGetValue(id, out var value) || value < deliveriesPerMessage)
            {
                notProcessed++;
            } else if (value > deliveriesPerMessage)
            {
                duplicateProcessed++;
            }
        }
        Console.WriteLine($"Not Processed: {notProcessed}");
        Console.WriteLine($"Duplicate Processed: {duplicateProcessed}");
        
        return (notProcessed == 0);
    }
}

[TestFixture]
class CosmosTests
{
    private CosmosBus _publisher;
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
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _publisher.CleanUp();
    }

    [TearDown]
    public void TearDown()
    {
        ProcessedMessages.dict = new ConcurrentDictionary<string, int>();
    }

    [Test]
    public async Task AllCommandsProcessed()
    {
        const int numMessages = 1000; // 65s / 10000
        //Send some commands
        SampleCosmosCommand[] messages = new SampleCosmosCommand[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new SampleCosmosCommand() { MessageBody = $"msg data {i}" };
            messageContents[i] = messages[i].MessageBody;
        }
        await _publisher.SendAsync(messages, CancellationToken.None);
        
        var startTime = DateTime.UtcNow;
        while (!ProcessedMessages.AllMessagesInDictionary(messageContents) && DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(120))
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        ProcessedMessages.AllMessagesInDictionary(messageContents).Should().BeTrue();
    }
    
    [Test]
    public async Task AllEventsProcessedWhenOneSubscriber()
    {
        const int numMessages = 1000; //1000 - 6.46s
        
        OneSubCosmosEvent[] messages = new OneSubCosmosEvent[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new OneSubCosmosEvent() { MessageBody = $"msg data {i}" };
            messageContents[i] = messages[i].MessageBody;
        }
        
        await _publisher.PublishAsync(messages, CancellationToken.None);
        
        var startTime = DateTime.UtcNow;
        while (!ProcessedMessages.AllMessagesInDictionary(messageContents) && DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        ProcessedMessages.AllMessagesInDictionary(messageContents).Should().BeTrue();
    }
    
    [Test]
    public async Task AllEventsProcessedWhenToTwoSubscribers()
    {
        const int numMessages = 1000;
        
        TwoSubCosmosEvent[] messages = new TwoSubCosmosEvent[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new TwoSubCosmosEvent() { MessageBody = $"msg data {i}" };
            messageContents[i] = messages[i].MessageBody;
        }
        await _publisher.PublishAsync(messages, CancellationToken.None);
        
        var startTime = DateTime.UtcNow;
        while (!ProcessedMessages.AllMessagesInDictionary(messageContents,2) && DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        ProcessedMessages.AllMessagesInDictionary(messageContents,2).Should().BeTrue();
    }

    [Test]
    public async Task FailedEventsShouldBeRetriedSetNumberOfTimes()
    {
        const int numMessages = 100;
        
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
        ProcessedMessages.AllMessagesInDictionary(messageContents,deadLetterLimit).Should().BeTrue();
    }

    
    
    //Terrible test since it's random if the messages fail or not meaning that there is a slight risk of a message failing
    // Should be converted to checking if each message is either sucessfully processed or in the deadletter Queue
    // Session consistency means reading if the message is in DL guarantees correctness
    [Test]
    public async Task AllEventsShouldBeSucessfullyProcessed()
    {
        const int numMessages = 1000;
        
        EventWithErrors[] messages = new EventWithErrors[numMessages];
        string[] messageContents = new string[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new EventWithErrors() { Body = $"msg data {i}" };
            messageContents[i] = messages[i].Body;
        }
        
        await _publisher.PublishAsync(messages, CancellationToken.None);
        
        var startTime = DateTime.UtcNow;
        while (!ProcessedMessages.AllMessagesInDictionary(messageContents) && DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        ProcessedMessages.AllMessagesInDictionary(messageContents).Should().BeTrue();
    }
}
