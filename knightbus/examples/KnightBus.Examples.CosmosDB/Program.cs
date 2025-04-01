using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using KnightBus.Core.DependencyInjection;
using KnightBus.Cosmos;
using KnightBus.Cosmos.Messages;
using Microsoft.Extensions.Hosting;
using KnightBus.Core;
using KnightBus.Host;
using KnightBus.Messages;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace KnightBus.Examples.CosmosDB;

class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting CosmosDB example");

        //Connection string should be saved as environment variable named "CosmosString"
        string? connectionString = Environment.GetEnvironmentVariable("CosmosString"); 
        const string databaseId = "PubSub";
        const string leaseContainer = "Lease";

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
                        configuration.Database = databaseId;
                        configuration.LeaseContainer = leaseContainer;
                        configuration.PollingDelay = TimeSpan.FromMilliseconds(500);
                        configuration.DefaultTimeToLive = TimeSpan.FromSeconds(120);
                    })
                    .RegisterProcessors(typeof(Program).Assembly) //Can be any class name in this project
                    .UseTransport<CosmosTransport>();
            })
            .UseKnightBus()
        .Build();
        
        //Start the KnightBus Host
        await knightBusHost.StartAsync();
        await Task.Delay(TimeSpan.FromSeconds(1));
        Console.WriteLine("Started host");
        
        var client = knightBusHost.Services.CreateScope().ServiceProvider.GetRequiredService<CosmosBus>();

        const int numMessages = 100;
        //Send some commands
        SampleCosmosCommand[] messages = new SampleCosmosCommand[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            messages[i] = new SampleCosmosCommand() { MessageBody = $"msg data {i}" };
        }
        await client.SendAsync(messages, CancellationToken.None);

        Console.ReadKey();
        //Publish event
        SampleCosmosEvent[] events = new SampleCosmosEvent[numMessages];
        for (int i = 0; i < numMessages; i++)
        {
            events[i] = new SampleCosmosEvent() { MessageBody = $"msg data {i}" }; 
        }
        await client.PublishAsync(events, CancellationToken.None);
  

        Console.ReadKey();
        
        //Publish poison event
        await client.PublishAsync(new SamplePoisonEvent() { Bad_Message = $"danger" }, CancellationToken.None);
        
        //Clean-up
        client.CleanUp();
        Console.WriteLine("End of program, press any key to exit.");
        Console.ReadKey();
    }
}

class CosmosEventProcessor :
    IProcessEvent<SampleCosmosEvent, SampleSubscription, CosmosProcessingSetting>,
    IProcessEvent<SampleCosmosEvent, OtherSubscription, CosmosProcessingSetting>,
    IProcessEvent<SamplePoisonEvent, SamplePoisonSubscription, CosmosProcessingSetting>
{
    private Random random = new Random(); //Probably not ideal way to do this
    public Task ProcessAsync(SampleCosmosEvent message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Sub1: '{message.MessageBody}'");
        if (random.Next() % 5 == 0)
        {
            throw new HttpRequestException("Simulated network error");
        }
        return Task.CompletedTask;
    }
    
    public Task ProcessAsync(SamplePoisonEvent message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Poison sub: {message.Bad_Message}");
        throw new InvalidOperationException();
    }
}

class CosmosProcessingSetting : IProcessingSettings
{
    public int MaxConcurrentCalls => 10; //Currently not used
    public int PrefetchCount => 50; //Currently not used
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5); //Currently not used
    public int DeadLetterDeliveryLimit => 2;
}


//Sample event
public class SampleCosmosEvent : ICosmosEvent
{
    public string? MessageBody { get; set;  }
}
class SampleCosmosEventMapping : IMessageMapping<SampleCosmosEvent>
{
    public string QueueName => "test-topic";
}
class SampleSubscription: IEventSubscription<SampleCosmosEvent>
{
    public string Name => "subscription_1";
}

class OtherSubscription: IEventSubscription<SampleCosmosEvent>
{
    public string Name => "subscription_2";
}


//Poison event

public class SamplePoisonEvent : ICosmosEvent
{
    public required string Bad_Message { get; set;  }
}
class SamplePoisonEventMapping : IMessageMapping<SamplePoisonEvent>
{
    public string QueueName => "poison-topic";
}
class SamplePoisonSubscription: IEventSubscription<SamplePoisonEvent>
{
    public string Name => "poison_subscription_1";
}

class OtherSamplePoisonSubscription: IEventSubscription<SamplePoisonEvent>
{
    public string Name => "poison_subscription_2";
}


//Sample Command
class SampleCosmosCommand : ICosmosCommand
{
    public required string MessageBody { get; set; }
}

class SampleCosmosMessageMapping : IMessageMapping<SampleCosmosCommand>
{
    public string QueueName => "cosmos_sample_message";
}

class PostgresCommandProcessor :
    IProcessCommand<SampleCosmosCommand, CosmosProcessingSetting>
{
    public Task ProcessAsync(SampleCosmosCommand command, CancellationToken cancellationToken)
    {
        Console.WriteLine($"commandHandler 1: '{command.MessageBody}'");
        return Task.CompletedTask;
    }
}

