using KnightBus.Core;
using KnightBus.Core.DependencyInjection;
using KnightBus.Cosmos;
using KnightBus.Cosmos.Messages;
using KnightBus.Host;
using KnightBus.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KnightBus.Examples.CosmosDB;

class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting CosmosDB example");

        //Connection string should be saved as environment variable named "CosmosString"
        string? connectionString = Environment.GetEnvironmentVariable("CosmosString");

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
                        configuration.PollingDelay = TimeSpan.FromSeconds(2);
                        configuration.DefaultTimeToLive = TimeSpan.FromSeconds(120);
                        configuration.ClientOptions = new CosmosClientOptions()
                        {
                            AllowBulkExecution = true,
                            MaxRetryAttemptsOnRateLimitedRequests = 200,
                            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60),
                        };
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

        var client = knightBusHost
            .Services.CreateScope()
            .ServiceProvider.GetRequiredService<CosmosBus>();

        //Send some commands
        SampleCosmosCommand[] messages = new SampleCosmosCommand[1000];
        for (int i = 0; i < 1000; i++)
        {
            messages[i] = new SampleCosmosCommand() { MessageBody = $"data {i}" };
        }
        await client.SendAsync(messages, CancellationToken.None);

        //Publish some events
        SampleCosmosEvent[] events = new SampleCosmosEvent[1000];
        for (int i = 0; i < 1000; i++)
        {
            events[i] = new SampleCosmosEvent() { MessageBody = $"msg data {i}" };
        }
        await client.PublishAsync(events, CancellationToken.None);

        Console.ReadKey();

        //Publish poison events
        SamplePoisonEvent[] poisonEvents = new SamplePoisonEvent[10];
        //Publish poison event
        for (int i = 0; i < 10; i++)
        {
            poisonEvents[i] = new SamplePoisonEvent() { Bad_Message = $"danger {i}" };
        }
        await client.PublishAsync(poisonEvents, CancellationToken.None);

        //Clean-up
        Console.WriteLine("End of program, press any key to exit.");
        Console.ReadKey();
        client.Dispose();
    }
}

class CosmosEventProcessor
    : IProcessEvent<SampleCosmosEvent, SampleSubscription, CosmosProcessingSetting>,
        IProcessEvent<SamplePoisonEvent, SamplePoisonSubscription, CosmosProcessingSetting>,
        IProcessEvent<SamplePoisonEvent, OtherSamplePoisonSubscription, CosmosProcessingSetting>
{
    public Task ProcessAsync(SampleCosmosEvent message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Sub1: '{message.MessageBody}'");
        return Task.CompletedTask;
    }

    public Task ProcessAsync(SamplePoisonEvent message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Poison sub: {message.Bad_Message}");
        throw new InvalidOperationException();
    }
}

class OtherCosmosEventProcessor
    : IProcessEvent<SampleCosmosEvent, OtherSubscription, CosmosProcessingSetting>
{
    public Task ProcessAsync(SampleCosmosEvent message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Sub2: '{message.MessageBody}'");
        return Task.CompletedTask;
    }
}

class CosmosProcessingSetting : IProcessingSettings
{
    public int MaxConcurrentCalls => 10; //TODO Remove
    public int PrefetchCount => 50; //TODO remove
    public TimeSpan MessageLockTimeout => TimeSpan.FromMinutes(5); //TODO remove
    public int DeadLetterDeliveryLimit => 2;
}

//Sample event
public class SampleCosmosEvent : ICosmosEvent
{
    public string? MessageBody { get; set; }
}

class SampleCosmosEventMapping : IMessageMapping<SampleCosmosEvent>
{
    public string QueueName => "test-topic";
}

class SampleSubscription : IEventSubscription<SampleCosmosEvent>
{
    public string Name => "subscription_1";
}

class OtherSubscription : IEventSubscription<SampleCosmosEvent>
{
    public string Name => "subscription_2";
}

//Poison event

public class SamplePoisonEvent : ICosmosEvent
{
    public required string Bad_Message { get; set; }
}

class SamplePoisonEventMapping : IMessageMapping<SamplePoisonEvent>
{
    public string QueueName => "poison-topic";
}

class SamplePoisonSubscription : IEventSubscription<SamplePoisonEvent>
{
    public string Name => "poison_subscription_1";
}

class OtherSamplePoisonSubscription : IEventSubscription<SamplePoisonEvent>
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

class PostgresCommandProcessor : IProcessCommand<SampleCosmosCommand, CosmosProcessingSetting>
{
    public Task ProcessAsync(SampleCosmosCommand command, CancellationToken cancellationToken)
    {
        Console.WriteLine($"commandHandler 1: '{command.MessageBody}'");
        return Task.CompletedTask;
    }
}
